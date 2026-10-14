using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

// Short offline social exchanges, not open-ended generative chat. Never invent
// facts, read telemetry, or execute an action from a casual conversation.
public static class WendyConversation
{
    public static bool RadioCheck(string text) => Regex.IsMatch(WendyNaturalLanguage.Request(text),
        @"^(?:(?:(?:a |the )?(?:radio|audio|sound|voice|mic(?:rophone)?) (?:check|test|cheque)(?: one two)?|check (?:the |my )?(?:radio|audio|sound|microphone))|(?:(?:can |do )?you )?(?:hear|read) me(?: clearly| now)?|(?:are )?you there|are you (?:listening|online)|testing(?: one two(?: three)?)?)$");

    public static string? Reply(string text) {
        var q=WendyNaturalLanguage.Request(text);
        return q switch {
            "hello" or "hi" or "hey" or "hello there" or "good morning" or "good evening" or "good afternoon" => "Hi. Good to hear you.",
            "how are you" or "how are you doing" or "how is it going" or "hows it going" or "how was your day" => "I'm ready and listening. How are you doing?",
            "im good" or "im fine" or "im doing well" or "pretty good" or "great" or "not bad" => "Good to hear.",
            "im tired" or "i am tired" or "i had a long day" => "Sounds like a break would help. Take your time.",
            "im nervous" or "im frustrated" or "im stressed" or "i had a bad day" => "That sounds tough. Take a breath; there's no rush.",
            "im happy" or "i am happy" or "i won" or "that was fun" => "That's great. Enjoy the moment.",
            "thanks" or "thank you" or "thank you wendy" or "good job" or "nice work" or "you are helpful" => "You're welcome.",
            "goodbye" or "bye" or "see you later" or "good night" => "See you later. Take care.",
            "a joke" or "a joke to me" or "say something funny" or "make me laugh" => "Why did the computer get cold? It left its Windows open.",
            "who are you" or "what are you" or "are you a real person" or "whats your name" => "I'm Wendy, your virtual F1 racing assistant.",
            "lets chat" or "i want to chat" or "can we chat" or "talk to me" or "lets talk" => "Of course. How has your day been?",
            "what can we talk about" or "what can you do" => "I can share a joke, offer encouragement, or have a short chat. With F1 telemetry, I can give race updates.",
            "wish me luck" => "Good luck. You've got this.",
            "what is your favorite car" or "whats your favourite car" or "do you like racing" => "I don't drive, but helping you race is my job.",
            "are you ready" or "ready" or "lets go" => "Ready when you are.",
            "what time is it" or "what is the time" => $"It's {DateTime.Now:h mm tt} on your PC.",
            _ => null
        };
    }
}
