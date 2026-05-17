using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DSTALGO.StudyGroup
{
    public class QuizSelect
    {
        public static void Main(string[] args)
        {
            Quiz myQuiz = new Quiz();

            Console.WriteLine("DSTALGO QUIZ 1 PRACTICE");
            myQuiz.ShuffleQuestions();
            myQuiz.QuizStart();
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
        string rawText = "";
        string hiddenJsonPath = "quiz_lifetime_stats.json";

        public Quiz()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string filePath = "questions.json";
            rawText = File.ReadAllText(filePath);
            question = JsonSerializer.Deserialize<List<Question>>(rawText, options);
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
            // === ADDED: Track how many questions were actually answered before exiting ===
            int questionsAttempted = 0;
            mistakeReport.Clear();

            foreach (Question q in question)
            {
                Console.Clear();

                Console.WriteLine($"{q.questionText}");
                Console.Write("\n> ");

                string userInput = Console.ReadLine();

                if (userInput != null)
                {
                    // Clean the input to check for the exit command
                    string cleanInput = userInput.Trim().ToLower();

                    // === NEW: INSTANT EXIT CHECK ===
                    if (cleanInput == "exit")
                    {
                        break; // Immediately drops out of the foreach loop!
                    }
                    // ===============================

                    questionsAttempted++; // Increment because they submitted a real answer
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

                        MistakeLog log = new MistakeLog();
                        log.QuestionText = q.questionText;
                        log.UserAnswer = userInput;
                        log.CorrectAnswer = q.correctAnswer;

                        mistakeReport.Add(log);
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\nEXPLANATION:");
                    Console.ResetColor();
                    Console.WriteLine(q.explanation);

                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
            }

            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("                QUIZ COMPLETE!                    ");
            Console.WriteLine("==================================================");
            // === CHANGED: Score dynamically scales based on questions attempted vs total database size ===
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
                Console.WriteLine("No mistakes made in this session! ");
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
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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
                reportBuilder.AppendLine("             LIFETIME MISTAKE REPORT              ");
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

                File.WriteAllText("quiz_lifetime_report.txt", reportBuilder.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to update stats: {ex.Message}");
            }
        }
    }
}