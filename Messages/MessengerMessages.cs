using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;

namespace MauMind.App.Messages;

public sealed class WebSearchRequestedMessage : ValueChangedMessage<string>
{
    public WebSearchRequestedMessage(string query) : base(query) { }
}

public sealed class WebSearchDeniedMessage : ValueChangedMessage<bool>
{
    public WebSearchDeniedMessage() : base(false) { }
}

public sealed class FollowUpQuestionsMessage : ValueChangedMessage<List<string>>
{
    public FollowUpQuestionsMessage(List<string> questions) : base(questions) { }
}

public sealed class StreamingCompletedMessage : ValueChangedMessage<bool>
{
    public StreamingCompletedMessage() : base(true) { }
}
