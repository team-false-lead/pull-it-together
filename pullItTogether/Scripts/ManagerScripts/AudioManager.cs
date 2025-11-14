using Godot;
using System;

public partial class AudioManager : Node
{
    [Export]
    private AudioStreamPlayer audioPlayer;

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public void StartMusic()
    {
        audioPlayer.Play();
    }

    public void ToggleLayer(int layer, bool enabled)
    {
        AudioStreamSynchronized stream = (AudioStreamSynchronized)audioPlayer.Stream;
        if (enabled)
            stream.SetSyncStreamVolume(layer, 0);
        else
            stream.SetSyncStreamVolume(layer, -60);
    }
}
