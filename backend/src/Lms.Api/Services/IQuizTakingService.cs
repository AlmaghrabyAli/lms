using Lms.Api.Dtos.Quizzes;

namespace Lms.Api.Services;

public interface IQuizTakingService
{
    Task<QuizResult<List<QuizListItemDto>>> ListForCourseAsync(int courseId, int userId, string role);
    Task<QuizResult<QuizListItemDto>> GetAsync(int quizId, int userId, string role);
    Task<QuizResult<AttemptStartDto>> StartAttemptAsync(int quizId, int userId);
    Task<QuizResult<AttemptResultDto>> SubmitAsync(int attemptId, int userId, SubmitAttemptRequest request);
    Task<QuizResult<AttemptResultDto>> GetResultAsync(int attemptId, int userId);
    Task<QuizResult<MyAttemptsDto>> MyAttemptsAsync(int quizId, int userId);
}