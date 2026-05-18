using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

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

                // Programmatically sort topics to ensure requested ordering requirements:
                // Basics -> First, Coding -> Second to last, Miscellaneous -> Dead last.
                topics.Sort((x, y) =>
                {
                    int GetSortWeight(string id)
                    {
                        string cleanId = id.ToLower().Trim();
                        if (cleanId.Contains("basics")) return 1;
                        if (cleanId.Contains("coding")) return 98;
                        if (cleanId.Contains("miscellaneous") || cleanId.Contains("misc")) return 99;
                        return 50; // Standard items rest safely in the middle ground
                    }

                    int weightX = GetSortWeight(x.topicId);
                    int weightY = GetSortWeight(y.topicId);

                    if (weightX != weightY) return weightX.CompareTo(weightY);
                    return string.Compare(x.displayName, y.displayName, StringComparison.OrdinalIgnoreCase);
                });
            }

            while (true)
            {
                // Fetch latest version of recent scores history file on menu load
                Dictionary<string, string> scoreHistory = LoadScoreHistory();

                Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("             DSTALGO QUIZ SYSTEM MENU             ");
                Console.WriteLine("==================================================");

                // Menu Width is exactly 50 characters to align flush against header borders
                const int menuWidth = 50;

                for (int i = 0; i < topics.Count; i++)
                {
                    var topic = topics[i];
                    string scoreSuffix = scoreHistory.ContainsKey(topic.topicId) ? $" ({scoreHistory[topic.topicId]})" : "";

                    string leftText = $"{i + 1}. {topic.displayName}";
                    string rightText = $"({topic.totalQuestions} Qs){scoreSuffix}";
                    int paddingNeeded = menuWidth - leftText.Length;

                    if (paddingNeeded > rightText.Length)
                    {
                        Console.WriteLine($"{leftText}{rightText.PadLeft(paddingNeeded)}");
                    }
                    else
                    {
                        Console.WriteLine($"{leftText} {rightText}");
                    }
                }

                // Format the Master Combined Pool Option to be flush right with history check
                string mixSuffix = scoreHistory.ContainsKey("randomized_mix") ? $" ({scoreHistory["randomized_mix"]})" : "";
                string mixLeft = $"{topics.Count + 1}. RANDOMIZED MIX";
                string mixRight = $"(All Topics Combined){mixSuffix}";
                int mixPadding = menuWidth - mixLeft.Length;

                if (mixPadding > mixRight.Length)
                {
                    Console.WriteLine($"{mixLeft}{mixRight.PadLeft(mixPadding)}");
                }
                else
                {
                    Console.WriteLine($"{mixLeft} {mixRight}");
                }

                Console.WriteLine("Type 'exit' at any prompt to quit the application.");
                Console.WriteLine("==================================================");
                Console.Write("Select an option: ");

                string selectionInput = Console.ReadLine();
                if (selectionInput == null) return;

                string menuCheck = selectionInput.Trim().ToLower();
                if (menuCheck == "exit")
                {
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

        // Constructor for Single Topic Execution
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

        // Constructor for Unified Global Mixed Pool Execution
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
                Console.Write("\n> ");

                string userInput = Console.ReadLine();
                if (userInput == null) continue;

                string cleanInput = userInput.Trim().ToLower();

                if (cleanInput == "exit")
                {
                    break;
                }

                questionsAttempted++;
                string cleanAnswer = q.correctAnswer.Trim().ToLower();

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
                    Console.WriteLine($"The correct answer is: {q.correctAnswer}");

                    MistakeLog log = new MistakeLog
                    {
                        questionText = q.questionText,
                        userAnswer = userInput,
                        correctAnswer = q.correctAnswer
                    };
                    mistakeReport.Add(log);

                    // === INTEGRATED FEATURE: REWRITE CORRECTION 3 TIMES ===
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n[Reinforcement] You must type the correct answer 3 times in a row to proceed.");
                    Console.ResetColor();

                    int consecutiveCorrectStrikes = 0;
                    bool exitTriggered = false;

                    while (consecutiveCorrectStrikes < 3)
                    {
                        Console.Write($"[{consecutiveCorrectStrikes + 1}/3] Rewrite answer: ");
                        string rewriteInput = Console.ReadLine();

                        if (rewriteInput == null) continue;

                        string cleanRewrite = rewriteInput.Trim().ToLower();

                        if (cleanRewrite == "exit")
                        {
                            exitTriggered = true;
                            break;
                        }

                        if (cleanRewrite == cleanAnswer)
                        {
                            consecutiveCorrectStrikes++;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"-> Mistake made! Resetting counter. Match target text exactly: \"{q.correctAnswer}\"");
                            Console.ResetColor();
                            consecutiveCorrectStrikes = 0;
                        }
                    }

                    if (exitTriggered)
                    {
                        break;
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("-> Correction completed successfully.");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nEXPLANATION:");
                Console.ResetColor();
                Console.WriteLine(q.explanation);

                Console.WriteLine("\nPress any key to continue (or type 'exit' at next question pointer)...");
                Console.ReadKey();
            }

            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("                 QUIZ COMPLETE!                   ");
            Console.WriteLine("==================================================");
            Console.WriteLine($" Your final score: ({correctCount}/{questionsAttempted})  [Total pool size: {question.Count}]");
            Console.WriteLine("==================================================\n");

            // Calculate and record recent history tracking parameters
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
                    Console.WriteLine($"    Your Answer:    \"{m.userAnswer}\"");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Correct Answer: \"{m.correctAnswer}\"");
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

                System.Text.StringBuilder reportBuilder = new System.Text.StringBuilder();
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

                using (StreamWriter writer = new StreamWriter(absoluteReportPath, false, System.Text.Encoding.UTF8))
                {
                    writer.Write(reportBuilder.ToString());
                    writer.Flush();
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = absoluteReportPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
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