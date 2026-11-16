using Exam.Core.Domain;
using Exam.Core.DTOs.QuestionBank;
using Exam.Core.Enums;
using Exam.Core.Exceptions;
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

            var qnums = rows.Select(r => r.Entity.Qnum).Where(q => !string.IsNullOrWhiteSpace(q)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

            var figuredPool = await _context.QuestionBank
                .AsNoTracking()
                .Where(q => q.FigureUrl != null && q.FigureUrl != "")
                .Select(q => new { q.Text, q.FigureUrl, q.MainTopic })
                .ToListAsync(cancellationToken);
            var figuredIncoming = rows
                .Where(r => BankFigures.HasFigure(r.Entity.FigureUrl))
                .Select(r => r.Entity)
                .ToList();

            var insertedRows = new List<QuestionBankImportRow>();
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

                if (!BankFigures.HasFigure(row.Entity.FigureUrl)
                    && (figuredIncoming.Any(f => f.Qnum != row.Entity.Qnum
                            && BankFigures.IsTextOnlyDuplicate(row.Entity.Text, row.Entity.FigureUrl, row.Entity.MainTopic, f.Text, f.FigureUrl, f.MainTopic))
                        || figuredPool.Any(f => BankFigures.IsTextOnlyDuplicate(row.Entity.Text, row.Entity.FigureUrl, row.Entity.MainTopic, f.Text, f.FigureUrl, f.MainTopic))))
                {
                    result.Skipped++;
                    result.Errors.Add($"Sətir {row.Line} ({row.Entity.Qnum}): şəkilli sualın mətn nüsxəsi atıldı.");
                    continue;
                }

                if (existingByQnum.TryGetValue(row.Entity.Qnum, out var current))
                {
                    CopyBankFields(row.Entity, current);
                    var opts = existingOptions.Where(o => o.QuestionBankId == current.Id).ToList();
                    _context.QuestionBankOptions.RemoveRange(opts);
                    foreach (var o in row.Options)
                        o.QuestionBankId = current.Id;
                    _context.QuestionBankOptions.AddRange(row.Options);
                    result.Updated++;
                }
                else
                {
                    _context.QuestionBank.Add(row.Entity);
                    existingByQnum[row.Entity.Qnum] = row.Entity;
                    insertedRows.Add(row);
                    result.Inserted++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var row in insertedRows)
            {
                foreach (var o in row.Options)
                    o.QuestionBankId = row.Entity.Id;
                if (row.Options.Count > 0)
                    _context.QuestionBankOptions.AddRange(row.Options);
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
            var studentWeekly = kind == PaperKind.Weekly
                || PaperQuotas.IsStudentWeeklyExam(exam.Id, exam.Title);
            var allowSituation = PaperQuotas.AllowSituation(kind, quota.Audience);
            var allowIsbat = PaperQuotas.AllowIsbat(kind, quota.Audience);
            var audienceFilter = PaperQuotas.NormalizeAudience(quota.Audience)
                ?? PaperQuotas.AudienceFromKind(kind);
            var ignoreExamDifficulty = PaperQuotas.IgnoreExamDifficulty(kind, quota.Audience, studentWeekly);

            await ReplaceOrConflictAsync(examId, cancellationToken);

            var paper = ResolveQuota(quota, kind, exam.TotalQuestions);

            if (!allowSituation)
                paper = new PaperQuota { Mcq = paper.Mcq, OpenCoded = paper.OpenCoded, Written = paper.Written, Situation = 0, FillAny = paper.FillAny };

            var query = _context.QuestionBank.AsNoTracking().AsQueryable();
            if (audienceFilter is "weekly" or "teacher")
            {
                query = query.Where(q => q.Audience != null && q.Audience != "");
            }
            var wantedGrades = BankGrades.Parse(quota.Grade);
            if (wantedGrades.Count == 1)
            {
                var grade = wantedGrades[0];
                query = query.Where(q =>
                    q.Grade == null || q.Grade == ""
                    || q.Grade == grade
                    || q.Grade.StartsWith(grade + ",")
                    || q.Grade.EndsWith("," + grade)
                    || q.Grade.Contains("," + grade + ","));
            }
            if (!ignoreExamDifficulty && quota.DifficultyStrict == true && !string.IsNullOrWhiteSpace(quota.DifficultyLevel))
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
            if (wantedGrades.Count > 0)
                pool = pool.Where(q => BankGrades.IsBlank(q.Grade) || BankGrades.Overlaps(q.Grade, wantedGrades)).ToList();
            if (!string.IsNullOrWhiteSpace(audienceFilter))
                pool = pool.Where(q => PaperQuotas.BankAudienceFits(q.Audience, audienceFilter)).ToList();
            if (!allowIsbat)
                pool = pool.Where(q => !IsIsbat(q)).ToList();
            if (!allowSituation)
                pool = pool.Where(q => string.IsNullOrWhiteSpace(q.SituationId)).ToList();

            var copyCounts = await LoadCopyCountsAsync(cancellationToken);
            var usedQnums = studentWeekly
                ? await LoadWeeklyUsedQnumsAsync(exam.Id, quota.ExcludeQnums, cancellationToken)
                : MergeUsed(copyCounts, quota.ExcludeQnums);
            var selected = SelectPool(
                pool,
                paper,
                allowSituation,
                copyCounts,
                mixDifficulties: ignoreExamDifficulty,
                quota,
                usedQnums,
                out var usedDifficultyFallback);
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

            var pending = new List<(Question Question, QuestionBank Bank)>();
            foreach (var bank in selected)
            {
                var question = new Question
                {
                    ExamId = examId,
                    Text = ExamQuestionText(bank),
                    Type = bank.Type,
                    InputKind = string.IsNullOrWhiteSpace(bank.InputKind)
                        ? (bank.Type == QuestionType.OpenEnded ? "Text" : "Choice")
                        : bank.InputKind,
                    CorrectText = bank.CorrectText,
                    Points = bank.Points > 0 ? bank.Points : 1,
                    FigureUrl = BankFigures.First(bank.FigureUrl),
                    SourceQnum = bank.Qnum
                };
                _context.Questions.Add(question);
                pending.Add((question, bank));
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var (question, bank) in pending)
            {
                var slot = Classify(bank);
                var opts = bankOptions.Where(o => o.QuestionBankId == bank.Id).ToList();
                if (opts.Count > 0 && question.Type != QuestionType.OpenEnded)
                {
                    _context.QuestionOptions.AddRange(opts.Select(o => new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }));
                }

                added++;
                copied.Add(bank.Qnum);
                counts[slot] = counts[slot] + 1;
            }

            exam.TotalQuestions = await _context.Questions.CountAsync(q => q.ExamId == examId, cancellationToken);
            await TouchUsageAsync(selected.Select(s => s.Id).ToList(), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var warnings = new List<string>();
            if (ignoreExamDifficulty && !string.IsNullOrWhiteSpace(quota.DifficultyLevel))
                warnings.Add("Həftəlik sınaqda imtahan çətinliyi yoxdur; DifficultyLevel nəzərə alınmadı (sual çətinlikləri qarışıq).");
            if (usedDifficultyFallback)
                warnings.Add("Mövzu kvotasında çətin/orta tapılmadı; boş zolaq asan tərəfə endirildi (çətin→orta→asan).");
            if (studentWeekly && usedQnums is { Count: > 0 })
                warnings.Add($"Həftəlik kağız əvvəlki həftəlik/daily SourceQnum-ları keçdi ({usedQnums.Count} qnum).");
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

        private static List<QuestionBank> SelectPool(
            List<QuestionBank> pool,
            PaperQuota paper,
            bool allowSituation,
            IReadOnlyDictionary<string, int> copies,
            bool mixDifficulties,
            FromBankRequestDto quota,
            IReadOnlySet<string>? usedQnums,
            out bool usedDifficultyFallback)
        {
            var rng = Random.Shared;
            var used = new HashSet<int>();
            var result = new List<QuestionBank>();
            usedDifficultyFallback = false;

            if (allowSituation && paper.Situation > 0)
            {
                var groups = pool
                    .Where(q => !string.IsNullOrWhiteSpace(q.SituationId))
                    .GroupBy(q => q.SituationId!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderBy(x => x.Qnum).ToList())
                    .Where(g => g.Count > 0 && g.Count <= paper.Situation)
                    .ToList();

                var remaining = paper.Situation;
                while (remaining > 0)
                {
                    var fit = groups.Where(g => g.All(q => !used.Contains(q.Id)) && g.Count <= remaining).ToList();
                    if (fit.Count == 0) break;
                    var chosen = fit
                        .OrderBy(g => g.All(q => BankUsage.Unused(q, copies, usedQnums)) ? 0 : 1)
                        .ThenBy(g => g.Min(q => BankUsage.EffectiveUse(q, copies)))
                        .ThenBy(g => g.Min(q => q.LastUsedAt ?? DateTime.MinValue))
                        .ThenBy(g => g.Count == remaining ? 0 : 1)
                        .ThenByDescending(g => g.Count)
                        .ThenBy(_ => rng.Next())
                        .First();
                    foreach (var q in chosen)
                    {
                        if (used.Add(q.Id))
                            result.Add(q);
                    }
                    remaining -= chosen.Count;
                }
            }

            if (paper.FillAny)
            {
                var need = Math.Max(0, paper.Mcq - result.Count);
                var candidates = pool.Where(q => !used.Contains(q.Id) && Classify(q) != BankSlot.Situation);
                TakeWithBands(result, used, candidates, need, copies, rng, mixDifficulties, quota, usedQnums, ref usedDifficultyFallback);
                return result;
            }

            TakeSlot(result, used, pool, BankSlot.Mcq, paper.Mcq, copies, rng, mixDifficulties, quota, usedQnums, ref usedDifficultyFallback);
            TakeSlot(result, used, pool, BankSlot.OpenCoded, paper.OpenCoded, copies, rng, mixDifficulties, quota, usedQnums, ref usedDifficultyFallback);
            TakeSlot(result, used, pool, BankSlot.Written, paper.Written, copies, rng, mixDifficulties, quota, usedQnums, ref usedDifficultyFallback);
            return result;
        }

        private static void TakeSlot(
            List<QuestionBank> result,
            HashSet<int> used,
            List<QuestionBank> pool,
            BankSlot slot,
            int need,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            bool mixDifficulties,
            FromBankRequestDto quota,
            IReadOnlySet<string>? usedQnums,
            ref bool usedDifficultyFallback)
        {
            if (need <= 0) return;
            var candidates = pool.Where(q => !used.Contains(q.Id) && Classify(q) == slot);
            TakeWithBands(result, used, candidates, need, copies, rng, mixDifficulties, quota, usedQnums, ref usedDifficultyFallback);
        }

        private static void TakeWithBands(
            List<QuestionBank> result,
            HashSet<int> used,
            IEnumerable<QuestionBank> candidates,
            int need,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            bool mixDifficulties,
            FromBankRequestDto quota,
            IReadOnlySet<string>? usedQnums,
            ref bool usedDifficultyFallback)
        {
            var bands = BankDifficulty.Plan(
                need,
                mixDifficulties,
                quota.AsanCount,
                quota.OrtaCount,
                quota.CetinCount,
                quota.DifficultyLevel);
            List<QuestionBank> taken;
            if (bands == null)
            {
                taken = BankUsage.TakePreferred(candidates, need, result, copies, rng, usedQnums);
            }
            else
            {
                taken = BankDifficulty.Fill(
                    candidates, need, result, copies, rng, bands, out var fell,
                    strict: quota.DifficultyStrict == true,
                    usedQnums: usedQnums);
                if (fell) usedDifficultyFallback = true;
            }
            foreach (var q in taken)
            {
                used.Add(q.Id);
                result.Add(q);
            }
        }

        internal static string DifficultyBand(QuestionBank q) => BankDifficulty.Band(q);

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

        private async Task ReplaceOrConflictAsync(int examId, CancellationToken cancellationToken)
        {
            var existing = await _context.Questions
                .Where(q => q.ExamId == examId)
                .ToListAsync(cancellationToken);
            if (existing.Count == 0) return;

            var bankSourced = existing.Where(q => !string.IsNullOrWhiteSpace(q.SourceQnum)).ToList();
            if (bankSourced.Count == 0)
            {
                throw new FromBankConflictException(
                    "Bu imtahanda bankdan kopyalanmamış suallar var (SourceQnum yoxdur). Əl ilə əlavə olunanları silməmək üçün from-bank rədd edildi.");
            }

            var ids = bankSourced.Select(q => q.Id).ToList();
            var answered = await _context.StudentAnswers
                .AnyAsync(a => ids.Contains(a.QuestionId), cancellationToken);
            if (answered)
            {
                throw new FromBankConflictException(
                    "Bank suallarının artıq cavabları var; from-bank əvəzləməsi rədd edildi.");
            }

            var optionIds = await _context.QuestionOptions
                .Where(o => ids.Contains(o.QuestionId))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            if (optionIds.Count > 0)
            {
                var optionAnswers = await _context.StudentAnswers
                    .Where(a => a.SelectedOptionId != null && optionIds.Contains(a.SelectedOptionId.Value))
                    .ToListAsync(cancellationToken);
                if (optionAnswers.Count > 0)
                    throw new FromBankConflictException(
                        "Bank suallarının artıq cavabları var; from-bank əvəzləməsi rədd edildi.");
            }

            var options = await _context.QuestionOptions
                .Where(o => ids.Contains(o.QuestionId))
                .ToListAsync(cancellationToken);
            _context.QuestionOptions.RemoveRange(options);
            _context.Questions.RemoveRange(bankSourced);
            await _context.SaveChangesAsync(cancellationToken);
        }

        internal static string ExamQuestionText(QuestionBank bank)
        {
            var text = bank.Text ?? "";
            var title = bank.SituationTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(title)
                && !text.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                text = title + "\n\n" + text;
            }

            foreach (var figure in BankFigures.Parse(bank.FigureUrl))
            {
                if (text.Contains(figure, StringComparison.OrdinalIgnoreCase)) continue;
                text = text + "\n\n<img src=\"" + figure + "\" alt=\"figure\" />\n\n![figure](" + figure + ")";
            }

            return text;
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
            dest.Type = src.Type;
            dest.InputKind = src.InputKind;
            dest.CorrectText = src.CorrectText;
            dest.DifficultyLevel = src.DifficultyLevel;
            dest.Points = src.Points;
            dest.MainTopic = src.MainTopic;
            dest.Grade = Grade10Syllabus.Tag(BankGrades.Union(dest.Grade, src.Grade), src.MainTopic);
            dest.Subject = src.Subject;
            dest.Audience = src.Audience;
            dest.Language = src.Language;
            dest.SituationId = src.SituationId;
            dest.SituationTitle = src.SituationTitle;
            if (BankFigures.HasFigure(dest.FigureUrl) && !BankFigures.HasFigure(src.FigureUrl))
            {
                dest.Text = BankFigures.StudentFacingText(dest.Text, src.Text, dest.FigureUrl);
                return;
            }

            dest.Text = src.Text;
            dest.FigureUrl = BankFigures.Join(BankFigures.Parse(dest.FigureUrl).Concat(BankFigures.Parse(src.FigureUrl)));
        }

        private async Task<Dictionary<string, int>> LoadCopyCountsAsync(CancellationToken cancellationToken)
        {
            var rows = await _context.Questions
                .AsNoTracking()
                .Where(q => q.SourceQnum != null && q.SourceQnum != "")
                .Select(q => new { q.SourceQnum, q.ExamId })
                .ToListAsync(cancellationToken);
            return rows
                .GroupBy(r => r.SourceQnum!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ExamId).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<HashSet<string>> LoadWeeklyUsedQnumsAsync(
            int currentExamId,
            List<string>? extra,
            CancellationToken cancellationToken)
        {
            var exams = await _context.Exams
                .AsNoTracking()
                .Select(e => new { e.Id, e.Title })
                .ToListAsync(cancellationToken);
            var weeklyIds = exams
                .Where(e => e.Id != currentExamId && PaperQuotas.IsStudentWeeklyExam(e.Id, e.Title))
                .Select(e => e.Id)
                .ToList();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (weeklyIds.Count > 0)
            {
                var qnums = await _context.Questions
                    .AsNoTracking()
                    .Where(q => weeklyIds.Contains(q.ExamId) && q.SourceQnum != null && q.SourceQnum != "")
                    .Select(q => q.SourceQnum!)
                    .ToListAsync(cancellationToken);
                foreach (var q in qnums)
                    set.Add(q);
            }
            AddExclude(set, extra);
            return set;
        }

        private static HashSet<string>? MergeUsed(IReadOnlyDictionary<string, int> copies, List<string>? extra)
        {
            if (extra == null || extra.Count == 0) return null;
            var set = new HashSet<string>(copies.Keys, StringComparer.OrdinalIgnoreCase);
            AddExclude(set, extra);
            return set;
        }

        private static void AddExclude(HashSet<string> set, List<string>? extra)
        {
            if (extra == null) return;
            foreach (var raw in extra)
            {
                var q = (raw ?? "").Trim();
                if (q.Length > 0) set.Add(q);
            }
        }

        private async Task TouchUsageAsync(List<int> bankIds, CancellationToken cancellationToken)
        {
            if (bankIds.Count == 0) return;
            var rows = await _context.QuestionBank
                .Where(q => bankIds.Contains(q.Id))
                .ToListAsync(cancellationToken);
            var qnums = rows.Select(r => r.Qnum).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var fresh = await _context.Questions
                .AsNoTracking()
                .Where(q => q.SourceQnum != null && qnums.Contains(q.SourceQnum))
                .Select(q => new { q.SourceQnum, q.ExamId })
                .ToListAsync(cancellationToken);
            var counts = fresh
                .GroupBy(r => r.SourceQnum!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ExamId).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                counts.TryGetValue(row.Qnum, out var n);
                row.UseCount = n;
                row.LastUsedAt = now;
            }
        }

        private static void WarnIfShort(List<string> warnings, string label, int need, int got)
        {
            if (need > got)
                warnings.Add($"{label}: requested {need}, copied {got}.");
        }
    }
}
