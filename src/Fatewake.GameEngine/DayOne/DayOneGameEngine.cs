namespace Fatewake.GameEngine.DayOne;

public sealed class DayOneGameEngine : IGameEngine
{
    public ActionResolution Resolve(GameSnapshot state, CandidateAction action)
    {
        if (state.SurvivorDay != 1 || state.EventKey != "day-001-injured-stranger")
            return Reject("event_not_supported");

        return action.ActionType switch
        {
            "help_injured_stranger" => Help(),
            "call_from_safety" => CallFromSafety(),
            "stay_inside" => StayInside(),
            "investigate_radio" => InvestigateRadio(),
            _ => Reject("action_not_supported")
        };
    }

    private static ActionResolution Help() => new(
        true,
        "injured_stranger_helped",
        [new("relationship", "michelle.trust", "increased"), new("fact", "player.exposed_self_to_danger", "true")],
        [new("first_impression", "Personal", "michelle", 2, new Dictionary<string,string>{{"reason","helped_injured_stranger"}})],
        new Dictionary<string,string>{{"michelleReaction","The player stepped out to help."},{"injuredStranger","Received immediate assistance."}},
        "day1-v1");

    private static ActionResolution CallFromSafety() => new(
        true, "called_from_safety",
        [new("relationship","michelle.trust","slightly_increased")],
        [new("first_impression","Personal","michelle",1,new Dictionary<string,string>{{"reason","helped_from_safety"}})],
        new Dictionary<string,string>{{"michelleReaction","The player tried to help without stepping outside."}}, "day1-v1");

    private static ActionResolution StayInside() => new(
        true, "remained_inside",
        [new("relationship","michelle.trust","decreased")],
        [new("first_impression","Personal","michelle",2,new Dictionary<string,string>{{"reason","did_not_intervene"}})],
        new Dictionary<string,string>{{"michelleReaction","Michelle saw that no help came from the player's home."}}, "day1-v1");

    private static ActionResolution InvestigateRadio() => new(
        true, "radio_investigated",
        [new("knowledge","radio.future_timestamp","noticed")],
        [new("anomaly","Personal","radio",2,new Dictionary<string,string>{{"time","06:17"}})],
        new Dictionary<string,string>{{"radioTimestamp","The broadcast is timestamped tomorrow at 6:17 AM."}}, "day1-v1");

    private static ActionResolution Reject(string reason) => new(
        false, reason, [], [], new Dictionary<string,string>(), "day1-v1");
}
