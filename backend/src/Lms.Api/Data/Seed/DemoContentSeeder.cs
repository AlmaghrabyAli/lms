using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Data.Seed;

// Optional demo content for screenshots, the demo video and the live demo. OFF by default: switch it on with
// Seed:DemoContent=true (an environment variable Seed__DemoContent=true, or user-secrets locally).
// Idempotent: when the main demo course exists nothing is touched, so a restart never duplicates or overwrites.
public static class DemoContentSeeder
{
    private const string InstructorEmail = "instructor@lms.local";
    private const string MainSlug = "introduction-to-web-development";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!config.GetValue<bool>("Seed:DemoContent")) return;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoContentSeeder");

        if (await db.Courses.AnyAsync(c => c.Slug == MainSlug)) return;   // already seeded

        var instructor = await db.Users.FirstOrDefaultAsync(u => u.Email == InstructorEmail && u.Role == Roles.Instructor);
        if (instructor is null)
        {
            logger.LogWarning("Demo content was not seeded: the demo instructor {Email} does not exist.", InstructorEmail);
            return;
        }

        // ---- Course 1: the full demo course (2 modules, 5 lessons, a quiz and an assignment) ----
        var web = NewCourse(instructor, "Introduction to Web Development", MainSlug,
            "A practical introduction to how the web works. You will learn the basics of HTML, CSS and JavaScript while building a small personal page.",
            "Web Development", CourseLevel.Beginner, published: true);

        var gettingStarted = AddModule(web, "Getting started", 1);
        AddText(gettingStarted, 1, "How the web works",
            "## How the web works\n\nWhen you type an address into the browser, it sends a request to a server. The server answers with an HTML page, and the browser draws it on the screen.\n\n- **Browser**: the program that requests and shows pages.\n- **Server**: the computer that stores the page.\n- **HTTP**: the language they use to talk.",
            8);
        AddLink(gettingStarted, 2, "MDN: Learn web development", "https://developer.mozilla.org/en-US/docs/Learn", 15);
        AddText(gettingStarted, 3, "Your first HTML page",
            "## Your first HTML page\n\nCreate a file called `index.html` and write a heading and a paragraph about yourself. Open the file in your browser to see it.",
            10);

        var styling = AddModule(web, "Styling and behaviour", 2);
        AddText(styling, 1, "CSS basics",
            "## CSS basics\n\nCSS describes how a page looks: colours, fonts and spacing. A rule has a selector and a block of declarations.",
            10);
        AddText(styling, 2, "A first taste of JavaScript",
            "## A first taste of JavaScript\n\nJavaScript makes a page interactive. It can react to clicks, change text and talk to servers.",
            12);

        var quiz = new Quiz
        {
            Course = web,
            Module = styling,
            Title = "Web basics quiz",
            Description = "Check what you learned about the web, HTML and CSS.",
            PassingScorePercent = 60,
            MaxAttempts = 3,
            IsPublished = true
        };
        quiz.Questions.Add(Question(1, "Which language describes the structure of a web page?", QuestionType.SingleChoice, 1,
            ("HTML", true), ("CSS", false), ("Python", false), ("SQL", false)));
        quiz.Questions.Add(Question(2, "Which of these run in the browser?", QuestionType.MultipleChoice, 2,
            ("HTML", true), ("CSS", true), ("JavaScript", true), ("SQL Server", false)));
        quiz.Questions.Add(Question(3, "CSS is used to style web pages.", QuestionType.TrueFalse, 1,
            ("True", true), ("False", false)));

        var assignment = new Assignment
        {
            Course = web,
            Module = gettingStarted,
            Title = "Build your first web page",
            Instructions = "Create a simple HTML page that introduces yourself: a heading, a short paragraph and one image or link.\n\nPublish it anywhere you like (for example GitHub Pages) and submit the link, or paste your HTML in the text box.",
            DueDate = DateTime.UtcNow.AddDays(14),
            MaxScore = 100,
            IsPublished = true
        };

        // ---- Courses 2 and 3: published, to give the catalog some variety ----
        var js = NewCourse(instructor, "JavaScript Essentials", "javascript-essentials",
            "The core ideas of JavaScript: variables, functions and arrays. A short course for people who already know a little HTML.",
            "Programming", CourseLevel.Intermediate, published: true);
        var jsCore = AddModule(js, "Core concepts", 1);
        AddText(jsCore, 1, "Variables and types", "## Variables and types\n\nUse `let` and `const` to store values. JavaScript has numbers, strings, booleans, arrays and objects.", 8);
        AddText(jsCore, 2, "Functions", "## Functions\n\nA function groups steps you want to reuse. Give it a name, parameters and a return value.", 10);
        AddText(jsCore, 3, "Working with arrays", "## Working with arrays\n\nArrays hold lists of values. `map`, `filter` and `reduce` transform them without loops.", 12);

        var py = NewCourse(instructor, "Python for Automation", "python-for-automation",
            "Use Python to automate everyday tasks: renaming files, reading spreadsheets and running scripts on a schedule.",
            "Programming", CourseLevel.Beginner, published: true);
        var pyTasks = AddModule(py, "Automating everyday tasks", 1);
        AddText(pyTasks, 1, "Why automate?", "## Why automate?\n\nAnything you do the same way more than three times is a candidate for a script.", 6);
        AddText(pyTasks, 2, "Working with files", "## Working with files\n\nThe `pathlib` module lets you list, rename and move files in a few lines.", 10);
        AddText(pyTasks, 3, "Scheduling scripts", "## Scheduling scripts\n\nOn Windows use Task Scheduler, on Linux use cron, to run your script automatically.", 9);

        // ---- Course 4: a draft, so the instructor can publish it live ----
        var react = NewCourse(instructor, "Advanced React Patterns", "advanced-react-patterns",
            "Composition, custom hooks and performance patterns for larger React applications. Still a draft.",
            "Web Development", CourseLevel.Advanced, published: false);
        var reactOutline = AddModule(react, "Draft outline", 1);
        AddText(reactOutline, 1, "Course outline", "## Course outline\n\nThis course is still being written.", 5);

        db.Courses.AddRange(web, js, py, react);
        db.Quizzes.Add(quiz);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();   // one SaveChanges: everything or nothing

        logger.LogInformation("Demo content seeded: 4 courses, 1 quiz and 1 assignment for {Email}.", InstructorEmail);
    }

    private static Course NewCourse(User instructor, string title, string slug, string description, string category, CourseLevel level, bool published) =>
        new()
        {
            Title = title,
            Slug = slug,
            Description = description,
            Category = category,
            Level = level,
            IsPublished = published,
            InstructorId = instructor.Id
        };

    private static Module AddModule(Course course, string title, int order)
    {
        var module = new Module { Title = title, OrderIndex = order };
        course.Modules.Add(module);
        return module;
    }

    private static void AddText(Module module, int order, string title, string content, int minutes) =>
        module.Lessons.Add(new Lesson { Title = title, Type = LessonType.Text, Content = content, DurationMinutes = minutes, OrderIndex = order });

    private static void AddLink(Module module, int order, string title, string url, int minutes) =>
        module.Lessons.Add(new Lesson { Title = title, Type = LessonType.Link, ExternalUrl = url, DurationMinutes = minutes, OrderIndex = order });

    private static Question Question(int order, string text, QuestionType type, int points, params (string Text, bool Correct)[] options)
    {
        var question = new Question { OrderIndex = order, Text = text, Type = type, Points = points };
        foreach (var option in options)
            question.Options.Add(new AnswerOption { Text = option.Text, IsCorrect = option.Correct });
        return question;
    }
}