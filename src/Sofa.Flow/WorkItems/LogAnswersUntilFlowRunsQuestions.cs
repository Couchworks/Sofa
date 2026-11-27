using Sofa.Contracts.Intake;
using Wolverine.Attributes;

namespace Sofa.Flow.WorkItems;

[WolverineHandler]
public static class LogAnswersUntilFlowRunsQuestions
{
    public static void Handle(QuestionAnsweredOnRequest answered, ILogger<QuestionAnsweredOnRequest> logger) =>
        logger.LogInformation(
            "Flow received the answer to Question {QuestionId} on Request {RequestId} from User {UserId}",
            answered.QuestionId, answered.RequestId, answered.AnsweredByUserId);
}
