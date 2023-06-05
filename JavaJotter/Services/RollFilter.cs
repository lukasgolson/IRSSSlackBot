using System.Text.RegularExpressions;
using JavaJotter.Interfaces;
using JavaJotter.Types;
using SlackNet.Events;

namespace JavaJotter.Services;

public partial class RollFilter : IRollFilter
{
    public Roll? ProcessMessage(Message messageEvent)
    {
        return TryExtractRoll(messageEvent, out var roll) ? roll : null;
    }


    /// <summary>
    ///     Extracts a roll from the message event if the message contains a properly formatted dice roll.
    /// </summary>
    /// <param name="messageEvent">The message event which may contain a Dicebot roll message.</param>
    /// <param name="result">
    ///     When this method returns, contains the Roll record corresponding
    ///     to the messageEvent if the extraction is successful. The Roll record includes the UserID,
    ///     timestamp, and the roll value. If the extraction fails, this output parameter is set to null.
    ///     The extraction fails if the first text string in the attachment of the messageEvent is not in the
    ///     required format.
    /// </param>
    /// <returns>
    ///     Returns 'true' if the roll was successfully extracted, and 'false' if the extraction failed
    ///     due to an improperly formatted message.
    /// </returns>
    /// <remarks>
    ///     The expected message format is:
    ///     '<![CDATA[<@SomeUserID> rolled *123*]]>'
    ///     where "SomeUserID" can be a combination of alphanumeric characters,
    ///     and "123" represents the roll value which must be an integer.
    /// </remarks>
    private static bool TryExtractRoll(Message messageEvent, out Roll? result)
    {
        result = null;

        var message = messageEvent.AttachmentTexts.FirstOrDefault();

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!RollFormatRegex().IsMatch(message)) return false;
        var taggedUser = TaggedUserRegex().Match(message).Groups[1].Value;
        var rolls = ExtractRollValueRegex().Match(message).Groups[1].Value;


        if (!int.TryParse(rolls, out var rollValue)) return false;
        result = new Roll(messageEvent.Timestamp, messageEvent.Channel, taggedUser, rollValue);
        return true;
    }

    [GeneratedRegex("<@[A-Za-z0-9]+>\\s+rolled\\s+\\*\\d+\\*")]
    private static partial Regex RollFormatRegex();

    [GeneratedRegex("<@([A-Za-z0-9]+)>")]
    private static partial Regex TaggedUserRegex();

    [GeneratedRegex("\\*(\\d+)\\*")]
    private static partial Regex ExtractRollValueRegex();
}