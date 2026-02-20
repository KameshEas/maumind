namespace MauMind.App.Services;

public interface IFollowUpService
{
    /// <summary>
    /// Generate up to 3 follow-up questions based on the user's query and AI response.
    /// Runs entirely on-device with no external calls.
    /// </summary>
    List<string> GenerateFollowUps(string userQuery, string aiResponse);
}
