using Lms.Api.Dtos.Quizzes;

namespace Lms.Api.Services;

public interface IInstructorQuizService
{
    // null = the course does not exist or belongs to another instructor (the controller answers 404).
    Task<List<QuizSummaryDto>?> ListAsync(int courseId, int instructorId);
    Task<QuizResult<QuizDetailDto>> CreateAsync(int courseId, int instructorId, CreateQuizRequest request);
    Task<QuizResult<QuizDetailDto>> GetAsync(int quizId, int instructorId);
    Task<QuizResult<QuizDetailDto>> UpdateAsync(int quizId, int instructorId, UpdateQuizRequest request);
    Task<QuizError> DeleteAsync(int quizId, int instructorId);
    Task<QuizResult<QuizDetailDto>> SetPublishedAsync(int quizId, int instructorId, bool publish);
    Task<QuizResult<List<QuizStudentResultDto>>> ListResultsAsync(int quizId, int instructorId);
    Task<QuizResult<QuestionDto>> AddQuestionAsync(int quizId, int instructorId, SaveQuestionRequest request);
    Task<QuizResult<QuestionDto>> UpdateQuestionAsync(int questionId, int instructorId, SaveQuestionRequest request);
    Task<QuizError> DeleteQuestionAsync(int questionId, int instructorId);
}