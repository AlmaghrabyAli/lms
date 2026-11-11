namespace Lms.Api.Entities;

public static class Roles
{
    public const string Student = "Student";
    public const string Instructor = "Instructor";
    public const string Admin = "Admin";

    public static readonly string[] All = [Student, Instructor, Admin];
}