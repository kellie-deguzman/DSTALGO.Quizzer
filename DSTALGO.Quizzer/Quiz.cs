using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace DSTALGO.StudyGroup
{
    public class QuizTopic
    {
        public string topicId { get; set; }
        public string displayName { get; set; }
        public string filePath { get; set; }
        public int totalQuestions { get; set; }
    }

    public class QuizSelect
    {
        private static string historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quiz_recent_scores.json");

        public static void Main(string[] args)
        {
            List<QuizTopic> topics = new List<QuizTopic>();
            var docOptions = new JsonDocumentOptions { AllowTrailingCommas = true };

            if (File.Exists("quiz-index.json"))
            {
                string indexJson = File.ReadAllText("quiz-index.json");
                topics = JsonSerializer.Deserialize<List<QuizTopic>>(indexJson);

                foreach (var topic in topics)
                {
                    if (File.Exists(topic.filePath))
                    {
                        string rawQuestions = File.ReadAllText(topic.filePath);
                        using (JsonDocument doc = JsonDocument.Parse(rawQuestions, docOptions))
                        {
                            topic.totalQuestions = doc.RootElement.GetArrayLength();
                        }
                    }
                }

                topics.Sort((x, y) =>
                {
                    int GetSortWeight(string id)
                    {
                        string cleanId = id.ToLower().Trim();

                        // 1. Core fundamentals first
                        if (cleanId.Contains("basics")) return 1;

                        // 2. Middle tiers sorted sequentially
                        if (cleanId.Contains("multidimensional")) return 10;
                        if (cleanId.Contains("jagged")) return 20;

                        // 3. Code implementations, exercises, and general items last
                        if (cleanId.Contains("coding")) return 98;
                        if (cleanId.Contains("miscellaneous") || cleanId.Contains("misc")) return 99;

                        return 50;
                    }

                    int weightX = GetSortWeight(x.topicId);
                    int weightY = GetSortWeight(y.topicId);

                    if (weightX != weightY) return weightX.CompareTo(weightY);
                    return string.Compare(x.displayName, y.displayName, StringComparison.OrdinalIgnoreCase);
                });
            }

            while (true)
            {
                Dictionary<string, string> scoreHistory = LoadScoreHistory();

                Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("             DSTALGO QUIZ SYSTEM MENU             ");
                Console.WriteLine("==================================================");

                const int menuWidth = 50;

                for (int i = 0; i < topics.Count; i++)
                {
                    var topic = topics[i];
                    string leftText = $"{i + 1}. {topic.displayName}";
                    string qCountText = $"({topic.totalQuestions} Qs)";
                    string rawScore = scoreHistory.ContainsKey(topic.topicId) ? scoreHistory[topic.topicId] : null;
                    string scoreSuffix = rawScore != null ? $" ({rawScore})" : "";

                    string totalRightText = $"{qCountText}{scoreSuffix}";
                    int paddingNeeded = menuWidth - leftText.Length;

                    if (paddingNeeded > totalRightText.Length)
                    {
                        Console.Write(leftText);
                        Console.Write(qCountText.PadLeft(paddingNeeded));
                        if (rawScore != null)
                        {
                            Console.Write(" (");
                            SetPercentageColor(rawScore);
                            Console.Write(rawScore);
                            Console.ResetColor();
                            Console.Write(")");
                        }
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.Write($"{leftText} {qCountText}");
                        if (rawScore != null)
                        {
                            Console.Write(" (");
                            SetPercentageColor(rawScore);
                            Console.Write(rawScore);
                            Console.ResetColor();
                            Console.Write(")");
                        }
                        Console.WriteLine();
                    }
                }

                // Render Randomized Mix Option
                string mixLeft = $"{topics.Count + 1}. RANDOMIZED MIX";
                string mixCountText = "(All Topics Combined)";
                string rawMixScore = scoreHistory.ContainsKey("randomized_mix") ? scoreHistory["randomized_mix"] : null;
                string mixSuffix = rawMixScore != null ? $" ({rawMixScore})" : "";

                string totalMixRightText = $"{mixCountText}{mixSuffix}";
                int mixPadding = menuWidth - mixLeft.Length;

                if (mixPadding > totalMixRightText.Length)
                {
                    Console.Write(mixLeft);
                    Console.Write(mixCountText.PadLeft(mixPadding));
                    if (rawMixScore != null)
                    {
                        Console.Write(" (");
                        SetPercentageColor(rawMixScore);
                        Console.Write(rawMixScore);
                        Console.ResetColor();
                        Console.Write(")");
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.Write($"{mixLeft} {mixCountText}");
                    if (rawMixScore != null)
                    {
                        Console.Write(" (");
                        SetPercentageColor(rawMixScore);
                        Console.Write(rawMixScore);
                        Console.ResetColor();
                        Console.Write(")");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("Type 'exit' at any prompt to quit the application.");
                Console.WriteLine("==================================================");
                Console.Write("Select an option: ");

                string selectionInput = Console.ReadLine();
                if (selectionInput == null) return;

                string menuCheck = selectionInput.Trim().ToLower();
                if (menuCheck == "exit")
                {
                    string absoluteReportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mistakes_report.txt");
                    if (File.Exists(absoluteReportPath))
                    {
                        try
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = absoluteReportPath,
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);
                        }
                        catch { }
                    }
                    break;
                }

                if (int.TryParse(selectionInput, out int choice) && choice > 0 && choice <= topics.Count + 1)
                {
                    Quiz myQuiz;
                    string activeTopicId = "";

                    if (choice == topics.Count + 1)
                    {
                        activeTopicId = "randomized_mix";
                        myQuiz = new Quiz(topics, activeTopicId);
                        Console.WriteLine("\nLoading combined dynamic pool...");
                    }
                    else
                    {
                        activeTopicId = topics[choice - 1].topicId;
                        myQuiz = new Quiz(topics[choice - 1], activeTopicId);
                        Console.WriteLine($"\nLoading {topics[choice - 1].displayName}...");
                    }

                    myQuiz.ShuffleQuestions();
                    myQuiz.QuizStart();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid entry. Press any key to try again...");
                    Console.ResetColor();
                    Console.ReadKey();
                }
            }
        }

        // Helper method to dynamically switch Console font colors depending on performance percentage threshold values
        private static void SetPercentageColor(string percentageString)
        {
            if (string.IsNullOrWhiteSpace(percentageString)) return;

            // Strip out non-numeric values (e.g. '%') to safely parse the score integer
            string digitsOnly = Regex.Replace(percentageString, @"[^\d]", "");
            if (int.TryParse(digitsOnly, out int score))
            {
                if (score >= 95)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else if (score >= 70)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
            }
        }

        private static Dictionary<string, string> LoadScoreHistory()
        {
            try
            {
                if (File.Exists(historyPath))
                {
                    string json = File.ReadAllText(historyPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    }
                }
            }
            catch { }
            return new Dictionary<string, string>();
        }
    }

    public class Question
    {
        public string questionText { get; set; }
        public string correctAnswer { get; set; }
        public string explanation { get; set; }
    }

    public class MistakeLog
    {
        public string questionText { get; set; }
        public string userAnswer { get; set; }
        public string correctAnswer { get; set; }
    }

    public class LifetimeStat
    {
        public string questionText { get; set; }
        public int timesWrong { get; set; }
        public string correctAnswer { get; set; }
        public List<string> wrongAnswers { get; set; } = new List<string>();
    }

    public class Quiz
    {
        List<Question> question = new List<Question>();
        List<MistakeLog> mistakeReport = new List<MistakeLog>();
        string hiddenJsonPath = "quiz_lifetime_stats.json";
        string currentTrackingId = "";
        private JsonSerializerOptions options;

        private void InitializeOptions()
        {
            options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };
        }

        public Quiz(QuizTopic targetTopic, string topicId)
        {
            InitializeOptions();
            currentTrackingId = topicId;

            if (File.Exists(targetTopic.filePath))
            {
                string rawText = File.ReadAllText(targetTopic.filePath);
                question = JsonSerializer.Deserialize<List<Question>>(rawText, options) ?? new List<Question>();
            }
        }

        public Quiz(List<QuizTopic> allTopics, string topicId)
        {
            InitializeOptions();
            currentTrackingId = topicId;

            foreach (var topic in allTopics)
            {
                if (File.Exists(topic.filePath))
                {
                    string rawText = File.ReadAllText(topic.filePath);
                    var subList = JsonSerializer.Deserialize<List<Question>>(rawText, options);
                    if (subList != null)
                    {
                        question.AddRange(subList);
                    }
                }
            }
        }

        public void ShuffleQuestions()
        {
            Random rd = new Random();
            for (int i = 0; i < question.Count; i++)
            {
                int randomIndex = rd.Next(0, question.Count);
                Question temp = question[i];
                question[i] = question[randomIndex];
                question[randomIndex] = temp;
            }
        }

        public void QuizStart()
        {
            int correctCount = 0;
            int questionsAttempted = 0;
            mistakeReport.Clear();

            foreach (Question q in question)
            {
                Console.Clear();
                Console.WriteLine($"{q.questionText}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(Write code in multiple lines. Press ENTER twice on an empty line to submit)");
                Console.ResetColor();
                Console.Write("\n> ");

                StringBuilder inputBuilder = new StringBuilder();
                string line;
                while (!string.IsNullOrEmpty(line = Console.ReadLine()))
                {
                    inputBuilder.AppendLine(line);
                }

                string rawUserInput = inputBuilder.ToString();
                string cleanInput = CleanWhitespace(rawUserInput);

                if (cleanInput == "exit")
                {
                    break;
                }

                questionsAttempted++;
                string cleanAnswer = CleanWhitespace(q.correctAnswer);

                if (cleanInput == cleanAnswer)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nCORRECT!");
                    Console.ResetColor();
                    correctCount++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nINCORRECT!");
                    Console.ResetColor();
                    Console.WriteLine("The correct answer structure is:");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(q.correctAnswer);
                    Console.ResetColor();

                    MistakeLog log = new MistakeLog
                    {
                        questionText = q.questionText,
                        userAnswer = rawUserInput.TrimEnd(),
                        correctAnswer = q.correctAnswer
                    };
                    mistakeReport.Add(log);
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nEXPLANATION:");
                Console.ResetColor();
                Console.WriteLine(string.IsNullOrWhiteSpace(q.explanation) ? "No dynamic explanation provided." : q.explanation);

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }

            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("                 QUIZ COMPLETE!                   ");
            Console.WriteLine("==================================================");
            Console.WriteLine($" Your final score: ({correctCount}/{questionsAttempted})  [Total pool size: {question.Count}]");
            Console.WriteLine("==================================================\n");

            if (questionsAttempted > 0)
            {
                double calculatedPercentage = ((double)correctCount / questionsAttempted) * 100;
                SaveRecentAttemptScore(currentTrackingId, $"{(int)Math.Round(calculatedPercentage)}%");
            }

            UpdateLifetimeDatabase();

            if (mistakeReport.Count > 0)
            {
                Console.WriteLine("REVIEW YOUR INCORRECT ANSWERS FROM THIS RUN:\n");
                for (int i = 0; i < mistakeReport.Count; i++)
                {
                    MistakeLog m = mistakeReport[i];
                    Console.WriteLine($"[{i + 1}] Question: {m.questionText}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    Your Answer:\n{m.userAnswer}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Correct Answer: {m.correctAnswer}");
                    Console.ResetColor();
                    Console.WriteLine(new string('-', 50));
                }
            }
            else if (questionsAttempted > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No mistakes made in this session! 😎");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("No questions were answered this session.");
            }

            Console.WriteLine("\nPress any key to return to the Main Menu.");
            Console.ReadKey();
        }

        private string CleanWhitespace(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string baseClean = input.Trim().ToLower();
            return Regex.Replace(baseClean, @"\s+", "");
        }

        private void SaveRecentAttemptScore(string topicId, string percentageString)
        {
            try
            {
                string historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quiz_recent_scores.json");
                Dictionary<string, string> scoreHistory = new Dictionary<string, string>();

                if (File.Exists(historyPath))
                {
                    string existingJson = File.ReadAllText(historyPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        scoreHistory = JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) ?? new Dictionary<string, string>();
                    }
                }

                scoreHistory[topicId] = percentageString;
                string updatedJson = JsonSerializer.Serialize(scoreHistory, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(historyPath, updatedJson);
            }
            catch { }
        }

        private void UpdateLifetimeDatabase()
        {
            try
            {
                List<LifetimeStat> lifetimeDatabase = new List<LifetimeStat>();
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    WriteIndented = true
                };

                if (File.Exists(hiddenJsonPath))
                {
                    string existingJson = File.ReadAllText(hiddenJsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        lifetimeDatabase = JsonSerializer.Deserialize<List<LifetimeStat>>(existingJson, jsonOptions) ?? new List<LifetimeStat>();
                    }
                }

                foreach (MistakeLog currentMistake in mistakeReport)
                {
                    LifetimeStat existingRecord = lifetimeDatabase.Find(s =>
                        string.Equals(s.questionText?.Trim(), currentMistake.questionText?.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (existingRecord != null)
                    {
                        existingRecord.timesWrong += 1;

                        bool alreadyGuessed = existingRecord.wrongAnswers.Exists(a =>
                            string.Equals(a.Trim(), currentMistake.userAnswer?.Trim(), StringComparison.OrdinalIgnoreCase));

                        if (!alreadyGuessed && !string.IsNullOrWhiteSpace(currentMistake.userAnswer))
                        {
                            existingRecord.wrongAnswers.Add(currentMistake.userAnswer);
                        }
                    }
                    else
                    {
                        LifetimeStat newRecord = new LifetimeStat
                        {
                            questionText = currentMistake.questionText,
                            timesWrong = 1,
                            correctAnswer = currentMistake.correctAnswer
                        };

                        if (!string.IsNullOrWhiteSpace(currentMistake.userAnswer))
                        {
                            newRecord.wrongAnswers.Add(currentMistake.userAnswer);
                        }

                        lifetimeDatabase.Add(newRecord);
                    }
                }

                string updatedJson = JsonSerializer.Serialize(lifetimeDatabase, jsonOptions);
                File.WriteAllText(hiddenJsonPath, updatedJson);

                StringBuilder reportBuilder = new StringBuilder();
                reportBuilder.AppendLine("==================================================");
                reportBuilder.AppendLine("             LIFETIME MISTAKE REPORT              ");
                reportBuilder.AppendLine("==================================================");
                reportBuilder.AppendLine();

                for (int i = 0; i < lifetimeDatabase.Count; i++)
                {
                    LifetimeStat stat = lifetimeDatabase[i];
                    if (stat == null) continue;

                    reportBuilder.AppendLine($"Question {i + 1} - \"{stat.questionText ?? "Unknown Question"}\"");
                    reportBuilder.AppendLine($"Number of times wrong: {stat.timesWrong}");
                    reportBuilder.AppendLine($"Correct answer:        {stat.correctAnswer ?? "N/A"}");

                    var answersList = stat.wrongAnswers ?? new List<string>();
                    string wrongAnswersJoined = string.Join(", ", answersList);

                    reportBuilder.AppendLine($"Wrong answers given:   [{wrongAnswersJoined}]");
                    reportBuilder.AppendLine(new string('-', 50));
                    reportBuilder.AppendLine();
                }

                string absoluteReportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mistakes_report.txt");

                using (StreamWriter writer = new StreamWriter(absoluteReportPath, false, Encoding.UTF8))
                {
                    writer.Write(reportBuilder.ToString());
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Warning] Report generation failed. Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
}