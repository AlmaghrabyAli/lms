using Lms.Api.Entities;
using Lms.Api.Services;
using Lms.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lms.Tests.Services;

public class AdminServiceTests
{
    [Fact]
    public async Task An_admin_cannot_change_their_own_role()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);

        var result = await new AdminService(db).ChangeRoleAsync(admin.Id, admin.Id, "Student");

        Assert.Equal(AdminError.SelfChange, result.Error);
        Assert.Equal(Roles.Admin, (await db.Users.FindAsync(admin.Id))!.Role);
    }

    [Fact]
    public async Task Changing_the_role_of_an_unknown_user_is_NotFound()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);

        var result = await new AdminService(db).ChangeRoleAsync(admin.Id, 99999, "Student");

        Assert.Equal(AdminError.NotFound, result.Error);
    }

    [Fact]
    public async Task An_instructor_who_still_owns_courses_cannot_be_demoted()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        await TestDb.AddCourseAsync(db, instructor);

        var result = await new AdminService(db).ChangeRoleAsync(admin.Id, instructor.Id, "Student");

        Assert.Equal(AdminError.OwnsCourses, result.Error);
        Assert.Equal(Roles.Instructor, (await db.Users.FindAsync(instructor.Id))!.Role);
    }

    [Fact]
    public async Task An_instructor_without_courses_can_be_demoted()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);

        var result = await new AdminService(db).ChangeRoleAsync(admin.Id, instructor.Id, "student");

        Assert.Equal(AdminError.None, result.Error);
        Assert.Equal(Roles.Student, result.Value!.Role);
    }

    [Fact]
    public async Task Promoting_a_student_works_and_repeating_it_is_harmless()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var service = new AdminService(db);

        var first = await service.ChangeRoleAsync(admin.Id, student.Id, "Instructor");
        var second = await service.ChangeRoleAsync(admin.Id, student.Id, "Instructor");

        Assert.Equal(Roles.Instructor, first.Value!.Role);
        Assert.Equal(AdminError.None, second.Error);
        Assert.Equal(Roles.Instructor, second.Value!.Role);
    }

    [Fact]
    public async Task An_admin_cannot_disable_themselves()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);

        var result = await new AdminService(db).SetActiveAsync(admin.Id, admin.Id, false);

        Assert.Equal(AdminError.SelfChange, result.Error);
        Assert.True((await db.Users.FindAsync(admin.Id))!.IsActive);
    }

    [Fact]
    public async Task Disabling_and_enabling_another_user_works()
    {
        await using var db = TestDb.Create();
        var admin = await TestDb.AddUserAsync(db, Roles.Admin);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var service = new AdminService(db);

        var disabled = await service.SetActiveAsync(admin.Id, student.Id, false);
        Assert.False(disabled.Value!.IsActive);
        Assert.False((await db.Users.FindAsync(student.Id))!.IsActive);

        var enabled = await service.SetActiveAsync(admin.Id, student.Id, true);
        Assert.True(enabled.Value!.IsActive);
    }

    [Fact]
    public async Task Unpublishing_hides_the_course_but_keeps_it_and_is_idempotent()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, instructor, published: true);
        var service = new AdminService(db);

        var first = await service.UnpublishCourseAsync(course.Id);
        var second = await service.UnpublishCourseAsync(course.Id);

        Assert.False(first.Value!.IsPublished);
        Assert.Equal(AdminError.None, second.Error);
        Assert.False((await db.Courses.FindAsync(course.Id))!.IsPublished);
    }

    [Fact]
    public async Task Unpublishing_an_unknown_course_is_NotFound()
    {
        await using var db = TestDb.Create();

        var result = await new AdminService(db).UnpublishCourseAsync(99999);

        Assert.Equal(AdminError.NotFound, result.Error);
    }

    [Fact]
    public async Task The_users_list_filters_by_role_status_and_search()
    {
        await using var db = TestDb.Create();
        await TestDb.AddUserAsync(db, Roles.Admin);
        await TestDb.AddUserAsync(db, Roles.Student);
        var inactiveStudent = await TestDb.AddUserAsync(db, Roles.Student);
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        inactiveStudent.IsActive = false;
        await db.SaveChangesAsync();
        var service = new AdminService(db);

        var students = await service.ListUsersAsync(null, Roles.Student, null, 1, 20);
        var inactive = await service.ListUsersAsync(null, null, false, 1, 20);
        var byName = await service.ListUsersAsync("Instructor", null, null, 1, 20);

        Assert.Equal(2, students.TotalCount);
        Assert.Equal(inactiveStudent.Id, Assert.Single(inactive.Items).Id);
        Assert.Equal(instructor.Id, Assert.Single(byName.Items).Id);
    }

    [Fact]
    public async Task The_users_list_clamps_the_page_size()
    {
        await using var db = TestDb.Create();

        var big = await new AdminService(db).ListUsersAsync(null, null, null, 0, 500);
        var none = await new AdminService(db).ListUsersAsync(null, null, null, 1, 0);

        Assert.Equal(1, big.Page);
        Assert.Equal(50, big.PageSize);
        Assert.Equal(20, none.PageSize);
    }
}