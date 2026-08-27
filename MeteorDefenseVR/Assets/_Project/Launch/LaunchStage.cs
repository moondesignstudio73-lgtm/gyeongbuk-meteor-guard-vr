namespace MeteorDefenseVR.Launch
{
    public enum LaunchStage
    {
        Idle,
        Hangar,
        DoorOpen,
        EngineStart,
        Countdown3,
        Countdown2,
        Countdown1,
        Launch,
        Space,
        MissionStart,
        Complete
    }

    public enum LaunchAudioCue
    {
        Door,
        Engine,
        Countdown,
        Launch,
        MissionStart
    }
}
