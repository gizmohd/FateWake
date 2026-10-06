namespace Fatewake.Web.Presentation;

/// <summary>Contains the authored scene definitions for the first day.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/DayOneScenes.md">DayOneScenes documentation</see>
public static class DayOneScenes
{
    /// <summary>The opening scene covering the initial day-one events and choices.</summary>
    public static readonly SceneDefinition Opening=new(
        "day1-0617-0643","the-silence-day1",1,"The Silence",
        [
            new("phone-0617",BeatKind.Establishing,"day1-phone-0617","6:17 AM","NO SERVICE","You wake to a phone that insists it has signal. Nothing connects.",null,[],false),
            new("house-flicker",BeatKind.Narration,"day1-house-flicker","6:18 AM",null,"The lights flicker twice. Somewhere outside, car alarms begin answering one another.",null,[],false),
            new("street-reveal",BeatKind.Dialogue,"day1-michelle-street","6:19 AM",null,"Across the threshold, Michelle is carrying most of an injured man's weight. Eli stands behind her.",new("MICHELLE SUMMERS","I need help!"),[],false),
            new("first-choice",BeatKind.Interaction,"day1-michelle-street","6:19 AM",null,null,null,[
                new("help_injured_stranger","Help Michelle"),
                new("call_from_safety","Call Out"),
                new("stay_inside","Stay Inside"),
                new("investigate_radio","Check Radio"),
                new("freeform","Do something else…",true)
            ]),
            new("choice-wake",BeatKind.Consequence,"day1-choice-wake","6:20 AM",null,"The moment passes, but the choice does not. It has already changed what happens next.",null,[]),
            new("radio-fragment",BeatKind.Dialogue,"day1-radio-fragment","6:31 AM","EMERGENCY BROADCAST","Static tears across the radio.",new("RADIO","…remain indoors. Do not attempt to—"),[]),
            new("timestamp",BeatKind.Anomaly,"day1-radio-timestamp","6:32 AM",null,"For a second, the display clears. The timestamp is tomorrow. 06:17.",null,[]),
            new("shutdown-0643",BeatKind.Transition,"day1-shutdown-0643","6:43 AM",null,"Every powered device dies at once. Not fades. Not flickers. Stops.",null,[]),
            new("silence",BeatKind.Narration,"day1-after-shutdown","6:43 AM",null,"The alarms are gone. The hum of the neighborhood is gone. In the sudden quiet, distance feels different.",null,[])
        ]);
}
