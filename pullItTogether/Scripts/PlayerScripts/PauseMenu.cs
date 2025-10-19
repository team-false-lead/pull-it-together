using Godot;
using System;

public partial class PauseMenu : Control
{
    // TO-DO: Make the pause menu a child of the player's UI once that
    // gets merged into main
    [Export]
    private Button resumeButton;
    [Export]
    private Button exitButton;

    public Button ResumeButton
    {
        get { return resumeButton; }
    }

    public Button ExitButton
    {
        get { return exitButton; }
    }

    public override void _Ready()
    {
        base._Ready();
        Visible = false;
    }
}
