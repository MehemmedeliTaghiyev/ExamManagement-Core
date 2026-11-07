using Exam.Core.Domain;
using Exam.Core.DTOs.QuestionBank;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Exam.Infrastructure.Banking;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class QuestionBankService : IQuestionBankService
    {
        private readonly ExamDbContext _context;
        private readonly CurrentTenant _tenant;

        public QuestionBankService(ExamDbContext context, CurrentTenant tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<QuestionBankImportResultDto> ImportCsvAsync(Stream csvStream, CancellationToken cancellationToken = default)
        {
            var result = new QuestionBankImportResultDto();
            var rows = QuestionBankCsv.Parse(csvStream);
            if (rows.Count == 0)
            {
                result.Errors.Add("CSV boşdur və ya başlıq sətri yoxdur.");
                return result;
            }

            var qnums = rows.Select(r => r.Entity.Qnum).Where(q => !string.IsNullOrWhiteSpace(q)).Distinct().ToList();
            var existing = await _context.QuestionBank
                .Where(q => qnums.Contains(q.Qnum))
                .ToListAsync(cancellationToken);
            var existingByQnum = existing.ToDictionary(q => q.Qnum, StringComparer.OrdinalIgnoreCase);
            var existingIds = existing.Select(q => q.Id).ToList();
            var existingOptions = existingIds.Count == 0
                ? new List<QuestionBankOption>()
                : await _context.QuestionBankOptions
                    .Where(o => existingIds.Contains(o.QuestionBankId))
                    .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Entity.Qnum))
                {
                    result.Skipped++;
                    result.Errors.Add($"Sətir {row.Line}: Qnum boşdur.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Entity.Text))
                {
                    result.Skipped++;
                    result.Errors.Add($"Sətir {row.Line} ({row.Entity.Qnum}): Text boşdur.");
                    continue;
                }

                if (existingByQnum.TryGetValue(row.Entity.Qnum, out var current))
                {
                    CopyBankFields(row.Entity, current);
                    var opts = existingOptions.Where(o => o.QuestionBankId == current.Id).ToList();
                    _context.QuestionBankOptions.RemoveRange(opts);
                    foreach (var o in row.Options)
                    {
                        o.QuestionBankId = current.Id;
                    }
                    _context.QuestionBankOptions.AddRange(row.Options);
                    result.Updated++;
                }
                else
                {
                    _context.QuestionBank.Add(row.Entity);
                    await _context.SaveChangesAsync(cancellationToken);
                    foreach (var o in row.Options)
                    {
                        o.QuestionBankId = row.Entity.Id;
                    }
                    if (row.Options.Count > 0)
                        _context.QuestionBankOptions.AddRange(row.Options);
                    existingByQnum[row.Entity.Qnum] = row.Entity;
                    result.Inserted++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return result;
        }

        public async Task<FromBankResultDto> CopyFromBankAsync(int examId, FromBankRequestDto quota, CancellationToken cancellationToken = default)
        {
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId, cancellationToken);
            if (exam == null || !_tenant.CanAccessExam(exam))
                throw new KeyNotFoundException("İmtahan tapılmadı.");

            quota ??= new FromBankRequestDto();
            var kind = PaperQuotas.ParseKind(quota.Kind, quota.Audience);
            var allowSituation = PaperQuotas.AllowSituation(kind, quota.Audience, quota.Grade);
            var allowIsbat = PaperQuotas.AllowIsbat(kind, quota.Audience);

            var paper = ResolveQuota(quota, kind, exam.TotalQuestions);

            if (!allowSituation)
                paper = new PaperQuota { Mcq = paper.Mcq, OpenCoded = paper.OpenCoded, Written = paper.Written, Situation = 0 };

            var query = _context.QuestionBank.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(quota.Audience))
            {
                var audience = quota.Audience.Trim();
                query = query.Where(q => q.Audience == audience || q.Audience == null || q.Audience == "");
            }
            if (!string.IsNullOrWhiteSpace(quota.Grade))
            {
                var grade = PaperQuotas.NormalizeGrade(quota.Grade);
                query = query.Where(q => q.Grade == null || q.Grade == "" || q.Grade.Contains(grade));
            }
            if (!string.IsNullOrWhiteSpace(quota.DifficultyLevel))
            {
                var levels = quota.DifficultyLevel
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                query = query.Where(q => q.DifficultyLevel != null && levels.Contains(q.DifficultyLevel));
            }
            if (quota.Topics is { Count: > 0 })
            {
                var topics = quota.Topics.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                if (topics.Count > 0)
                    query = query.Where(q => q.MainTopic != null && topics.Contains(q.MainTopic));
            }
            if (!string.IsNullOrWhiteSpace(quota.Subject))
            {
                var subject = quota.Subject.Trim();
                query = query.Where(q => q.Subject == null || q.Subject == "" || q.Subject == subject);
            }
            if (!string.IsNullOrWhiteSpace(quota.Language))
            {
                var lang = quota.Language.Trim();
                query = query.Where(q => q.Language == null || q.Language == "" || q.Language == lang);
            }

            var pool = await query.ToListAsync(cancellationToken);
            if (!allowIsbat)
                pool = pool.Where(q => !IsIsbat(q)).ToList();
            if (!allowSituation)
                pool = pool.Where(q => string.IsNullOrWhiteSpace(q.SituationId)).ToList();

            var selected = SelectPool(pool, paper, allowSituation);
            var selectedIds = selected.Select(q => q.Id).ToList();
            var bankOptions = selectedIds.Count == 0
                ? new List<QuestionBankOption>()
                : await _context.QuestionBankOptions
                    .AsNoTracking()
                    .Where(o => selectedIds.Contains(o.QuestionBankId))
                    .ToListAsync(cancellationToken);

            var added = 0;
            var copied = new List<string>();
            var counts = new Dictionary<BankSlot, int>
            {
                [BankSlot.Mcq] = 0,
                [BankSlot.OpenCoded] = 0,
                [BankSlot.Written] = 0,
                [BankSlot.Situation] = 0
            };

            foreach (var bank in selected)
            {
                var slot = Classify(bank);
                var question = new Question
                {
                    ExamId = examId,
                    Text = bank.Text,
                    Type = bank.Type,
                    InputKind = string.IsNullOrWhiteSpace(bank.InputKind)
                        ? (bank.Type == QuestionType.OpenEnded ? "Text" : "Choice")
                        : bank.InputKind,
                    CorrectText = bank.CorrectText,
                    Points = bank.Points > 0 ? bank.Points : 1,
                    FigureUrl = bank.FigureUrl,
                    SourceQnum = bank.Qnum
                };
                _context.Questions.Add(question);
                await _context.SaveChangesAsync(cancellationToken);

                var opts = bankOptions.Where(o => o.QuestionBankId == bank.Id).ToList();
                if (opts.Count > 0 && question.Type != QuestionType.OpenEnded)
                {
                    _context.QuestionOptions.AddRange(opts.Select(o => new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }));
                    await _context.SaveChangesAsync(cancellationToken);
                }

                added++;
                copied.Add(bank.Qnum);
                counts[slot] = counts[slot] + 1;
            }

            exam.TotalQuestions = await _context.Questions.CountAsync(q => q.ExamId == examId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var warnings = new List<string>();
            if (!paper.FillAny)
            {
                WarnIfShort(warnings, "MCQ", paper.Mcq, counts[BankSlot.Mcq]);
                WarnIfShort(warnings, "open coded", paper.OpenCoded, counts[BankSlot.OpenCoded]);
                WarnIfShort(warnings, "written", paper.Written, counts[BankSlot.Written]);
                WarnIfShort(warnings, "situation", paper.Situation, counts[BankSlot.Situation]);
            }
            else if (paper.Mcq > added)
            {
                warnings.Add($"requested {paper.Mcq}, copied {added}.");
            }

            return new FromBankResultDto
            {
                ExamId = examId,
                Added = added,
                Mcq = counts[BankSlot.Mcq],
                OpenCoded = counts[BankSlot.OpenCoded],
                Written = counts[BankSlot.Written],
                Situation = counts[BankSlot.Situation],
                CopiedQnums = copied,
                Warnings = warnings
            };
        }

        private static PaperQuota ResolveQuota(FromBankRequestDto quota, PaperKind kind, int examTotal)
        {
            var known = PaperQuotas.ForKnownKind(quota.Grade, kind);
            var mcq = quota.McqCount ?? known?.Mcq;
            var open = quota.OpenCodedCount ?? known?.OpenCoded;
            var written = quota.WrittenCount ?? known?.Written;
            var situation = quota.SituationCount ?? known?.Situation;

            if (mcq == null && open == null && written == null && situation == null)
            {
                var n = quota.Count is > 0 ? quota.Count.Value : (examTotal > 0 ? examTotal : 10);
                return new PaperQuota { Mcq = n, FillAny = true };
            }

            return new PaperQuota
            {
                Mcq = mcq ?? 0,
                OpenCoded = open ?? 0,
                Written = written ?? 0,
                Situation = situation ?? 0
            };
        }

        private static List<QuestionBank> SelectPool(List<QuestionBank> pool, PaperQuota paper, bool allowSituation)
        {
            var rng = Random.Shared;
            var used = new HashSet<int>();
            var result = new List<QuestionBank>();

            if (allowSituation && paper.Situation > 0)
            {
                var groups = pool
                    .Where(q => !string.IsNullOrWhiteSpace(q.SituationId))
                    .GroupBy(q => q.SituationId!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderBy(x => x.Qnum).ToList())
                    .OrderBy(_ => rng.Next())
                    .ToList();

                var sitCount = 0;
                foreach (var group in groups)
                {
                    if (sitCount >= paper.Situation) break;
                    if (sitCount + group.Count > paper.Situation && sitCount > 0) continue;
                    foreach (var q in group)
                    {
                        if (used.Add(q.Id))
                        {
                            result.Add(q);
                            sitCount++;
                        }
                    }
                }
            }

            if (paper.FillAny)
            {
                var need = Math.Max(0, paper.Mcq - result.Count);
                var candidates = pool
                    .Where(q => !used.Contains(q.Id) && Classify(q) != BankSlot.Situation)
                    .OrderBy(_ => rng.Next())
                    .Take(need)
                    .ToList();
                foreach (var q in candidates)
                {
                    used.Add(q.Id);
                    result.Add(q);
                }
                return result;
            }

            TakeSlot(result, used, pool, BankSlot.Mcq, paper.Mcq, rng);
            TakeSlot(result, used, pool, BankSlot.OpenCoded, paper.OpenCoded, rng);
            TakeSlot(result, used, pool, BankSlot.Written, paper.Written, rng);
            return result;
        }

        private static void TakeSlot(List<QuestionBank> result, HashSet<int> used, List<QuestionBank> pool, BankSlot slot, int need, Random rng)
        {
            if (need <= 0) return;
            var candidates = pool
                .Where(q => !used.Contains(q.Id) && Classify(q) == slot)
                .OrderBy(_ => rng.Next())
                .Take(need)
                .ToList();
            foreach (var q in candidates)
            {
                used.Add(q.Id);
                result.Add(q);
            }
        }

        internal static BankSlot Classify(QuestionBank q)
        {
            if (!string.IsNullOrWhiteSpace(q.SituationId))
                return BankSlot.Situation;
            if (QuestionBankCsv.IsOpenKind(q.InputKind) && !q.InputKind.Equals("Text", StringComparison.OrdinalIgnoreCase))
                return BankSlot.OpenCoded;
            if (q.Type == QuestionType.OpenEnded || q.InputKind.Equals("Text", StringComparison.OrdinalIgnoreCase))
                return BankSlot.Written;
            return BankSlot.Mcq;
        }

        internal static bool IsIsbat(QuestionBank q)
        {
            var blob = $"{q.MainTopic} {q.SituationTitle} {q.Text}";
            return blob.Contains("isbat", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("sübut", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("subut", StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyBankFields(QuestionBank src, QuestionBank dest)
        {
            dest.Text = src.Text;
            dest.Type = src.Type;
            dest.InputKind = src.InputKind;
            dest.CorrectText = src.CorrectText;
            dest.DifficultyLevel = src.DifficultyLevel;
            dest.Points = src.Points;
            dest.Grade = src.Grade;
            dest.Subject = src.Subject;
            dest.Audience = src.Audience;
            dest.MainTopic = src.MainTopic;
            dest.Language = src.Language;
            dest.SituationId = src.SituationId;
            dest.SituationTitle = src.SituationTitle;
            dest.FigureUrl = src.FigureUrl;
        }

        private static void WarnIfShort(List<string> warnings, string label, int need, int got)
        {
            if (need > got)
                warnings.Add($"{label}: requested {need}, copied {got}.");
        }
    }
}
