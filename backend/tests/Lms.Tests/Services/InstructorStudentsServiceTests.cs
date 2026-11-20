using Lms.Api.Entities;
using Lms.Api.Services;
using Lms.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lms.Tests.Services;

public class InstructorStudentsServiceTests
{
    [Fact]
    public async Task Another_instructors_course_looks_like_a_missing_course()
    {
        await using var db = TestDb.Create();
        var owner = await TestDb.AddUserAsync(db, Roles.Instructor);
        var other = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, owner);
        var service = new InstructorStudentsService(db);

        Assert.Null(await service.ListAsync(course.Id, other.Id, 1, 20));
        Assert.NotNull(await service.ListAsync(course.Id, owner.Id, 1, 20));
    }

    [Fact]
    public async Task Progress_is_computed_live_from_the_completed_lessons()
    {
        await using var db = TestDb.Create();
        var owner = await TestDb.AddUserAsync(db, Roles.Instructor);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var course = await TestDb.AddCourseAsync(db, owner, lessons: 2);
        await TestDb.EnrollAsync(db, student, course);
        var lessons = await db.Lessons.OrderBy(l => l.Id).ToListAsync();
        var service = new InstructorStudentsService(db);

        await TestDb.CompleteAsync(db, student, lessons[0]);
        var row = Assert.Single((await service.ListAsync(course.Id, owner.Id, 1, 20))!.Items);
        Assert.Equal(1, row.CompletedLessons);
        Assert.Equal(2, row.TotalLessons);
        Assert.Equal(50, row.ProgressPercent);
        Assert.False(row.IsCompleted);

        await TestDb.CompleteAsync(db, student, lessons[1]);
        row = Assert.Single((await service.ListAsync(course.Id, owner.Id, 1, 20))!.Items);
        Assert.Equal(100, row.ProgressPercent);
        Assert.True(row.IsCompleted);
    }

    [Fact]
    public async Task Paging_is_clamped_to_safe_values()
    {
        await using var db = TestDb.Create();
        var owner = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, owner);
        var service = new InstructorStudentsService(db);

        var big = (await service.ListAsync(course.Id, owner.Id, 0, 500))!;
        Assert.Equal(1, big.Page);
        Assert.Equal(50, big.PageSize);

        var none = (await service.ListAsync(course.Id, owner.Id, 1, 0))!;
        Assert.Equal(20, none.PageSize);
    }
}