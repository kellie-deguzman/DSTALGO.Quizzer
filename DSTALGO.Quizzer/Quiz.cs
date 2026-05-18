using System;
using System.Collections.Generic;
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
                    ;

                    int weightX = GetSortWeight(x.topicId);
                    int weightY = GetSortWeight(y.topicId);

                    if (weightX != weightY) return weightX.CompareTo(weightY);
                    return string.Compare(x.displayName, y.displayName, StringComparison.OrdinalIgnoreCase);
                });
            }

            while (true)
            {
                Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("             DSTALGO QUIZ SYSTEM MENU             ");
                Console.WriteLine("==================================================");

                // Menu Width is exactly 50 characters to align flush against header borders
                const int menuWidth = 50;

                for (int i = 0; i < topics.Count; i++)
                {
                    string leftText = $"{i + 1}. {topics[i].displayName}";
                    string rightText = $"({topics[i].totalQuestions} Qs)";
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

                // Format the Master Combined Pool Option to be flush right
                string mixLeft = $"{topics.Count + 1}. RANDOMIZED MIX";
                string mixRight = "(All Topics Combined)";
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

                    if (choice == topics.Count + 1)
                    {
                        myQuiz = new Quiz(topics);
                        Console.WriteLine("\nLoading combined dynamic pool...");
                    }
                    else
                    {
                        myQuiz = new Quiz(topics[choice - 1]);
                        Console.WriteLine($"\nLoading {topics[choice - 1].displayName}...");
                    }

                    myQuiz.ShuffleQuestions();
                    myQuiz.QuizStart();
                    break;
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
    }

    public class Question
    {
        public string questionText { get; set; }
        public string correctAnswer { get; set; }
        public string explanation { get; set; }
    }

    public class MistakeLog
    {
        public string QuestionText { get; set; }
        public string UserAnswer { get; set; }
        public string CorrectAnswer { get; set; }
    }

    public class LifetimeStat
    {
        public string QuestionText { get; set; }
        public int TimesWrong { get; set; }
        public string CorrectAnswer { get; set; }
        public List<string> WrongAnswers { get; set; } = new List<string>();
    }

    public class Quiz
    {
        List<Question> question = new List<Question>();
        List<MistakeLog> mistakeReport = new List<MistakeLog>();
        string hiddenJsonPath = "quiz_lifetime_stats.json";
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
        public Quiz(QuizTopic targetTopic)
        {
            InitializeOptions();

            if (File.Exists(targetTopic.filePath))
            {
                string rawText = File.ReadAllText(targetTopic.filePath);
                question = JsonSerializer.Deserialize<List<Question>>(rawText, options) ?? new List<Question>();
            }
        }

        // Constructor for Unified Global Mixed Pool Execution
        public Quiz(List<QuizTopic> allTopics)
        {
            InitializeOptions();

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
                        QuestionText = q.questionText,
                        UserAnswer = userInput,
                        CorrectAnswer = q.correctAnswer
                    };
                    mistakeReport.Add(log);
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

            UpdateLifetimeDatabase();

            if (mistakeReport.Count > 0)
            {
                Console.WriteLine("REVIEW YOUR INCORRECT ANSWERS FROM THIS RUN:\n");
                for (int i = 0; i < mistakeReport.Count; i++)
                {
                    MistakeLog m = mistakeReport[i];
                    Console.WriteLine($"[{i + 1}] Question: {m.QuestionText}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    Your Answer:    \"{m.UserAnswer}\"");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Correct Answer: \"{m.CorrectAnswer}\"");
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

            Console.WriteLine("\nPress any key to close the program.");
            Console.ReadKey();
        }

        private void UpdateLifetimeDatabase()
        {
            try
            {
                List<LifetimeStat> lifetimeDatabase = new List<LifetimeStat>();
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };

                if (File.Exists(hiddenJsonPath))
                {
                    string existingJson = File.ReadAllText(hiddenJsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        lifetimeDatabase = JsonSerializer.Deserialize<List<LifetimeStat>>(existingJson, jsonOptions);
                    }
                }

                foreach (MistakeLog currentMistake in mistakeReport)
                {
                    LifetimeStat existingRecord = lifetimeDatabase.Find(s => s.QuestionText == currentMistake.QuestionText);

                    if (existingRecord != null)
                    {
                        existingRecord.TimesWrong += 1;
                        if (!existingRecord.WrongAnswers.Contains(currentMistake.UserAnswer))
                        {
                            existingRecord.WrongAnswers.Add(currentMistake.UserAnswer);
                        }
                    }
                    else
                    {
                        LifetimeStat newRecord = new LifetimeStat
                        {
                            QuestionText = currentMistake.QuestionText,
                            TimesWrong = 1,
                            CorrectAnswer = currentMistake.CorrectAnswer
                        };
                        newRecord.WrongAnswers.Add(currentMistake.UserAnswer);
                        lifetimeDatabase.Add(newRecord);
                    }
                }

                string updatedJson = JsonSerializer.Serialize(lifetimeDatabase);
                File.WriteAllText(hiddenJsonPath, updatedJson);

                System.Text.StringBuilder reportBuilder = new System.Text.StringBuilder();
                reportBuilder.AppendLine("==================================================");
                reportBuilder.AppendLine("                 MISTAKES REPORT                  ");
                reportBuilder.AppendLine("==================================================");
                reportBuilder.AppendLine();

                for (int i = 0; i < lifetimeDatabase.Count; i++)
                {
                    LifetimeStat stat = lifetimeDatabase[i];
                    reportBuilder.AppendLine($"Question {i + 1} - \"{stat.QuestionText}\"");
                    reportBuilder.AppendLine($"Number of times wrong: {stat.TimesWrong}");
                    reportBuilder.AppendLine($"Correct answer:        {stat.CorrectAnswer}");
                    string wrongAnswersJoined = string.Join(", ", stat.WrongAnswers);
                    reportBuilder.AppendLine($"Wrong answers given:   [{wrongAnswersJoined}]");
                    reportBuilder.AppendLine(new string('-', 50));
                    reportBuilder.AppendLine();
                }

                File.WriteAllText("mistakes_report.txt", reportBuilder.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to update stats: {ex.Message}");
            }
        }
    }
}