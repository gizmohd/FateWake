namespace Fatewake.Web.Presentation;

public static class DayOneArt
{
    private static ArtLayer Full(string key,int z=0,double opacity=1)=>new(key,1,z,.5,.5,1,1,opacity);
    public static IReadOnlyList<ArtLayer> For(string artworkKey)=>artworkKey switch
    {
        "day1-phone-0617"=>[Full("bg-day1-bedroom-dark"),new("prop-phone-0617-noservice",1,20,.5,.43,.48,.35)],
        "day1-house-flicker"=>[Full("bg-day1-house-hallway"),Full("light-day1-power-flicker",90,.7)],
        "day1-michelle-street"=>[Full("bg-day1-neighborhood-player-threshold"),new("char-eli-day1-observing",1,20,.67,.52,.18,.31),new("char-injured-stranger-day1-supported",1,30,.58,.57,.34,.48),new("char-michelle-day1-supporting",1,40,.68,.56,.32,.49),Full("fg-player-threshold-shadow",80),Full("light-day1-early-morning",90,.65)],
        "day1-choice-wake"=>[Full("bg-day1-neighborhood-after-choice"),Full("light-day1-early-morning",90,.65)],
        "day1-radio-fragment"=>[Full("bg-day1-radio-room"),new("prop-radio-day1",1,30,.52,.54,.55,.35),Full("effect-radio-static",80,.5)],
        "day1-radio-timestamp"=>[Full("bg-day1-radio-room"),new("prop-radio-timestamp-tomorrow-0617",1,40,.52,.50,.62,.42)],
        "day1-shutdown-0643"=>[Full("bg-day1-blackout-0643"),Full("effect-power-collapse",80,.8)],
        "day1-after-shutdown"=>[Full("bg-day1-neighborhood-silent"),Full("light-day1-after-shutdown",90,.6)],
        _=>[]
    };
}
