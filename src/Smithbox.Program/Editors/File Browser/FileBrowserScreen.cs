using Hexa.NET.ImGui;
using StudioCore;
using StudioCore.Application;
using StudioCore.Editors.Common;
using StudioCore.Keybinds;
using System.Diagnostics;
using System.Numerics;

namespace StudioCore.Editors.FileBrowser;

// Credit to GoogleBen (https://github.com/googleben/Smithbox/tree/VFS)
public class FileBrowserScreen : EditorScreen
{
    public ProjectEntry Project;

    public ActionManager ActionManager = new();

    public FileViewHandler ViewHandler;

    public FileCommandQueue CommandQueue;
    public FileShortcuts Shortcuts;

    public FileToolView ToolView;

    public string EditorName => "File Browser##fileBrowserEditor";
    public string CommandEndpoint => "file";
    public string SaveType => "File";
    public string WindowName => "";
    public bool HasDocked { get; set; }

    public FileBrowserScreen(ProjectEntry project)
    {
        Project = project;

        Shortcuts = new(this, project);
        CommandQueue = new(this, project);

        ViewHandler = new(this, project);

        ToolView = new(this, project);
    }

    public void OnGUI(string[] commands)
    {
        var scale = DPI.UIScale();

        Shortcuts.Monitor();

        CommandQueue.Parse(commands);

        if (ImGui.BeginMenuBar())
        {
            FileMenu();
            EditMenu();
            ViewMenu();

            ImGui.EndMenuBar();
        }

        var dsid = ImGui.GetID("DockSpace_FileBrowser");
        ImGui.DockSpace(dsid, new Vector2(0, 0), ImGuiDockNodeFlags.None);

        ViewHandler.HandleViews();

        if (ViewHandler.ActiveView != null)
        {
            ToolView.Display();
        }
    }


    public void FileMenu()
    {
        if (ImGui.BeginMenu(LocalizationManager.Instance.Get("File")))
        {
            if (ImGui.MenuItem(LocalizationManager.Instance.Get("View Game Directory")))
            {
                Process.Start("explorer.exe", Project.Descriptor.DataPath);
            }

            if (ImGui.MenuItem(LocalizationManager.Instance.Get("View Project Directory")))
            {
                Process.Start("explorer.exe", Project.Descriptor.ProjectPath);
            }

            ImGui.EndMenu();
        }
    }

    public void EditMenu()
    {
        var activeView = ViewHandler.ActiveView;

        if (ImGui.BeginMenu(LocalizationManager.Instance.Get("Edit")))
        {
            if (activeView != null)
            {
                // Undo
                if (ImGui.MenuItem(LocalizationManager.Instance.Get("Undo"), $"{InputManager.GetHint(KeybindID.Undo)} / {InputManager.GetHint(KeybindID.Undo_Repeat)}"))
                {
                    if (activeView.ActionManager.CanUndo())
                    {
                        activeView.ActionManager.UndoAction();
                    }
                }

                // Undo All
                if (ImGui.MenuItem(LocalizationManager.Instance.Get("Undo All")))
                {
                    if (activeView.ActionManager.CanUndo())
                    {
                        activeView.ActionManager.UndoAllAction();
                    }
                }

                // Redo
                if (ImGui.MenuItem(LocalizationManager.Instance.Get("Redo"), $"{InputManager.GetHint(KeybindID.Redo)} / {InputManager.GetHint(KeybindID.Redo_Repeat)}"))
                {
                    if (activeView.ActionManager.CanRedo())
                    {
                        activeView.ActionManager.RedoAction();
                    }
                }
            }

            ImGui.EndMenu();
        }
    }

    public void ViewMenu()
    {
        if (ImGui.BeginMenu(LocalizationManager.Instance.Get("View")))
        {
            if (ImGui.MenuItem(LocalizationManager.Instance.Get("Tools")))
            {
                CFG.Current.Interface_FileBrowser_ToolView = !CFG.Current.Interface_FileBrowser_ToolView;
            }
            UIHelper.ShowActiveStatus(CFG.Current.Interface_FileBrowser_ToolView);

            ImGui.Separator();

            ViewHandler.DisplayMenu();

            ImGui.EndMenu();
        }
    }

}
