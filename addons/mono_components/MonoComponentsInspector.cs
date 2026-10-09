using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Godot;

// TODO: its unused
[AttributeUsage(AttributeTargets.Field |  AttributeTargets.Property)]
public class DefaultValue<[MustBeVariant] T>(T value) : Attribute;

[Tool]
public partial class MonoComponentsInspector : EditorInspectorPlugin
{
    public override bool _CanHandle(GodotObject @object)
        => @object is Node and not ComponentNode;

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is not Node node)
            return;

        EditorComponents editorComponents = ResourceLoader.Load<EditorComponents>(MonoComponents.ComponentsResourcePath, cacheMode: ResourceLoader.CacheMode.Ignore);
        Control componentEditorMenu = ResourceLoader.Load<PackedScene>("res://addons/mono_components/component_editor_menu.tscn", cacheMode: ResourceLoader.CacheMode.Ignore).Instantiate<Control>();
        Control cardHolder = componentEditorMenu.GetNode<Control>("%CardHolder");
        BaseButton createButton = componentEditorMenu.GetNode<BaseButton>("%CreateButton");
        PopupMenu createPopupMenu = componentEditorMenu.GetNode<PopupMenu>("CreatePopupMenu");

        Node scene = EditorInterface.Singleton.GetEditedSceneRoot();

        createButton.Pressed += () =>
        {
            createPopupMenu.Clear();

            var subclasses = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ComponentNode)))
                .Select((type, i) => new { Type = type, Index = i });

            foreach (var item in subclasses)
            {
                createPopupMenu.AddItem(item.Type.FullName, item.Index);
                createPopupMenu.SetItemMetadata(item.Index, item.Type.FullName!);
            }

            createPopupMenu.Popup((Rect2I)new Rect2(createButton.GetScreenPosition() + new Vector2(0, createButton.Size.Y), Vector2.Zero));
        };

        createPopupMenu.IdPressed += (id) =>
        {
            string componentName = createPopupMenu.GetItemMetadata((int)id).AsString();
            var assignment = new EditorComponentAssignment { ComponentName = componentName };
            editorComponents.AddAssignment(scene.SceneFilePath, scene.GetPathTo(node), assignment);
            ResourceSaver.Save(editorComponents, MonoComponents.ComponentsResourcePath);
            Update();
        };

        Update();

        AddCustomControl(componentEditorMenu);
        return;

        void Update()
        {
            foreach (var card in cardHolder.GetChildren())
                card.QueueFree();

            foreach (var assignment in editorComponents.GetAssignments(scene.SceneFilePath, scene.GetPathTo(node)))
            {
                var card = ResourceLoader.Load<PackedScene>("res://addons/mono_components/component_card.tscn").Instantiate<Control>();
                card.GetNode<FoldableContainer>("%FoldableContainer").Title = assignment.ComponentName;
                card.GetNode<Button>("%RemoveButton").Pressed += () =>
                {
                    editorComponents.RemoveAssignment(scene.SceneFilePath, scene.GetPathTo(node), assignment);
                    ResourceSaver.Save(editorComponents, MonoComponents.ComponentsResourcePath);
                    card.QueueFree();
                };

                var vBox = new VBoxContainer() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

                foreach (var prop in GetExportedProperties(assignment.ComponentName))
                {
                    var hBox = new HBoxContainer() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    var label = new Label { Text = prop.Name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    var editor = GetEditorForType(prop.PropertyType);

                    if (editor == null)
                    {
                        GD.PushWarning($"No component editor for {prop.PropertyType}");
                        continue;
                    }
                    editor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    
                    if (assignment.Exports.TryGetValue(prop.Name, out var savedValue))
                        SetEditorValue(editor, prop.PropertyType, savedValue);

                    ConnectEditorSignal(editor, () => {
                        Variant value = GetEditorValue(editor, prop.PropertyType);
                        assignment.Exports[prop.Name] = value;

                        ResourceSaver.Save(editorComponents, MonoComponents.ComponentsResourcePath);
                    });

                    hBox.AddChild(label);
                    hBox.AddChild(editor);
                    vBox.AddChild(hBox);
                }

                card.GetNode<FoldableContainer>("%FoldableContainer").AddChild(vBox);
                cardHolder.AddChild(card);
            }
        }
    }

    private IEnumerable<PropertyInfo> GetExportedProperties(string componentName)
    {
        Type? componentType = Assembly.GetExecutingAssembly().GetType(componentName);
        if (componentType != null)
            return componentType.GetProperties().Where(p => Attribute.IsDefined(p, typeof(ExportAttribute)));
        GD.PushError($"Couldn't find a component named '{componentName}'!");
        return [];
    }

    private static Control? GetEditorForType(Type type)
    {
        if (type == typeof(string)) return new LineEdit();
        if (type == typeof(bool)) return new CheckBox();
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            return new SpinBox { AllowLesser = true, AllowGreater = true, Step = 1 };
        if (type == typeof(float) || type == typeof(double))
            return new SpinBox { Step = 0.01, AllowLesser = true, AllowGreater = true };

        if (type.IsEnum)
        {
            var optionButton = new OptionButton();
            foreach (var val in Enum.GetValues(type))
            {
                optionButton.AddItem(val.ToString());
                optionButton.SetItemMetadata(optionButton.ItemCount - 1, (int)val);
            }
            return optionButton;
        }

        if (type == typeof(Color)) return new ColorPickerButton();
        if (type == typeof(NodePath)) return new LineEdit { PlaceholderText = "NodePath" };

        if (type == typeof(Vector2)) return CreateVectorEditor(2, isInteger: false);
        if (type == typeof(Vector3)) return CreateVectorEditor(3, isInteger: false);
        if (type == typeof(Vector4)) return CreateVectorEditor(4, isInteger: false);
        if (type == typeof(Vector2I)) return CreateVectorEditor(2, isInteger: true);
        if (type == typeof(Vector3I)) return CreateVectorEditor(3, isInteger: true);

        return typeof(Resource).IsAssignableFrom(type) ? new EditorResourcePicker() { BaseType = type.Name } : null;
    }

    private static HBoxContainer CreateVectorEditor(int components, bool isInteger)
    {
        var hbox = new HBoxContainer();
        for (int i = 0; i < components; i++)
        {
            var spinBox = new SpinBox
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AllowLesser = true,
                AllowGreater = true,
                Step = isInteger ? 1 : 0.01
            };
            hbox.AddChild(spinBox);
        }
        return hbox;
    }

    private static void SetEditorValue(Control editor, Type propType, Variant value)
    {
        switch (editor)
        {
            case LineEdit lineEdit:
                lineEdit.Text = value.ToString();
                break;
            case SpinBox spinBox:
                spinBox.Value = value.As<double>();
                break;
            case CheckBox checkBox:
                checkBox.ButtonPressed = value.As<bool>();
                break;
            case ColorPickerButton colorPicker:
                colorPicker.Color = value.As<Color>();
                break;
            case EditorResourcePicker resourcePicker:
                resourcePicker.EditedResource = value.As<Resource>();
                break;
            case OptionButton optionButton when propType.IsEnum:
            {
                int intValue = value.As<int>();
                for (int i = 0; i < optionButton.ItemCount; i++)
                {
                    if (optionButton.GetItemMetadata(i).As<int>() != intValue)
                        continue;
                    optionButton.Select(i);
                    break;
                }

                break;
            }
            case HBoxContainer vectorEditor:
            {
                var spinBoxes = vectorEditor.GetChildren().OfType<SpinBox>().ToList();
                if (propType == typeof(Vector2)) { var v = value.As<Vector2>(); spinBoxes[0].Value = v.X; spinBoxes[1].Value = v.Y; }
                else if (propType == typeof(Vector3)) { var v = value.As<Vector3>(); spinBoxes[0].Value = v.X; spinBoxes[1].Value = v.Y; spinBoxes[2].Value = v.Z; }
                else if (propType == typeof(Vector4)) { var v = value.As<Vector4>(); spinBoxes[0].Value = v.X; spinBoxes[1].Value = v.Y; spinBoxes[2].Value = v.Z; spinBoxes[3].Value = v.W; }
                else if (propType == typeof(Vector2I)) { var v = value.As<Vector2I>(); spinBoxes[0].Value = v.X; spinBoxes[1].Value = v.Y; }
                else if (propType == typeof(Vector3I)) { var v = value.As<Vector3I>(); spinBoxes[0].Value = v.X; spinBoxes[1].Value = v.Y; spinBoxes[2].Value = v.Z; }

                break;
            }
        }
    }

    private static Variant GetEditorValue(Control editor, Type propType)
    {
        switch (editor)
        {
            // Convert text back to the original property type
            case LineEdit lineEdit when propType == typeof(string):
                return lineEdit.Text;
            case LineEdit lineEdit when propType == typeof(NodePath):
                return new NodePath(lineEdit.Text);
            case SpinBox spinBox when propType == typeof(int):
                return (int)spinBox.Value;
            case SpinBox spinBox when propType == typeof(long):
                return (long)spinBox.Value;
            case SpinBox spinBox when propType == typeof(short):
                return (short)spinBox.Value;
            case SpinBox spinBox when propType == typeof(byte):
                return (byte)spinBox.Value;
            case SpinBox spinBox when propType == typeof(float):
                return (float)spinBox.Value;
            case SpinBox spinBox when propType == typeof(double):
                return spinBox.Value;
            case CheckBox checkBox:
                return checkBox.ButtonPressed;
            case ColorPickerButton colorPicker:
                return colorPicker.Color;
            case EditorResourcePicker resourcePicker:
                return resourcePicker.EditedResource;
            case OptionButton optionButton when propType.IsEnum:
                return optionButton.GetItemMetadata(optionButton.Selected);
            case HBoxContainer vectorEditor:
            {
                var spinBoxes = vectorEditor.GetChildren().OfType<SpinBox>().ToList();
                if (propType == typeof(Vector2)) return new Vector2((float)spinBoxes[0].Value, (float)spinBoxes[1].Value);
                if (propType == typeof(Vector3)) return new Vector3((float)spinBoxes[0].Value, (float)spinBoxes[1].Value, (float)spinBoxes[2].Value);
                if (propType == typeof(Vector4)) return new Vector4((float)spinBoxes[0].Value, (float)spinBoxes[1].Value, (float)spinBoxes[2].Value, (float)spinBoxes[3].Value);
                if (propType == typeof(Vector2I)) return new Vector2I((int)spinBoxes[0].Value, (int)spinBoxes[1].Value);
                if (propType == typeof(Vector3I)) return new Vector3I((int)spinBoxes[0].Value, (int)spinBoxes[1].Value, (int)spinBoxes[2].Value);
                break;
            }
        }

        return new Variant();
    }

    private static void ConnectEditorSignal(Control editor, Action saveAction)
    {
        switch (editor)
        {
            case LineEdit lineEdit:
                // Save when Enter is pressed or the control loses focus.
                lineEdit.TextSubmitted += _ => saveAction();
                lineEdit.FocusExited += saveAction;
                break;
            case SpinBox spinBox:
                // SpinBox ValueChanged fires too often (while dragging)
                // but FocusExited doesn't seem to work at all
                spinBox.ValueChanged += _ => saveAction();

                // spinBox.GetLineEdit().TextSubmitted += async _ => { await editor.ToSignal(editor.GetTree(), SceneTree.SignalName.ProcessFrame); saveAction(); };
                // spinBox.FocusExited += saveAction;
                break;
            case CheckBox checkBox:
                checkBox.Toggled += _ => saveAction();
                break;
            case ColorPickerButton colorPicker:
                colorPicker.ColorChanged += _ => saveAction();
                break;
            case EditorResourcePicker resourcePicker:
                resourcePicker.ResourceChanged += _ => saveAction();
                break;
            case OptionButton optionButton:
                optionButton.ItemSelected += _ => saveAction();
                break;
            case HBoxContainer vectorEditor:
            {
                foreach (var vectorSpinBox in vectorEditor.GetChildren().OfType<SpinBox>())
                {
                    vectorSpinBox.ValueChanged += _ => saveAction();
                    // vectorSpinBox.GetLineEdit().TextSubmitted += _ => saveAction();
                    // vectorSpinBox.FocusExited += saveAction;
                }

                break;
            }
        }
    }
}