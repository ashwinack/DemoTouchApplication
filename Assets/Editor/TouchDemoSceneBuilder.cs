using System.Linq;
using DemoTouch;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Generates the two-display building reveal kiosk: touch control panel on Display 1, full-screen 3D preview
/// on Display 2. The 3D camera renders into a RenderTexture shown on both displays.
/// </summary>
public static class TouchDemoSceneBuilder
{
    const string ScenePath = "Assets/Scenes/TouchDemo.unity";
    const int UILayer = 5;
    const float ExteriorFieldOfView = 50f;

    static readonly Color PanelBackground = new Color(0.055f, 0.07f, 0.1f);
    static readonly Color CardColor = new Color(0.13f, 0.16f, 0.22f);
    static readonly Color ActiveColor = new Color(0.18f, 0.45f, 0.9f);
    static readonly Color MutedText = new Color(0.6f, 0.66f, 0.76f);

    [MenuItem("Tools/Touch Demo/Build Scene")]
    static void BuildFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            BuildScene();
    }

    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var buildings = CityBuilder.Build(ExteriorFieldOfView, out var dayNight);

        var kiosk = new GameObject("Kiosk");
        var state = kiosk.AddComponent<KioskStateManager>();
        var director = kiosk.AddComponent<KioskCameraDirector>();
        var look = kiosk.AddComponent<InteriorLook>();
        Assign(state, "director", director);

        var previewCamera = BuildPreviewCamera(out var brain);
        BuildCameraRigs(director, brain, look, buildings);

        var display2Camera = UICamera("Display 2 Camera", -2000f, 1, Color.black);
        var display2Image = BuildDisplay2(display2Camera, state);
        var panelCamera = UICamera("Display 1 Camera", -1000f, 0, PanelBackground);
        var panelScaler = BuildControlPanel(panelCamera, state, dayNight, look, buildings, out var panelPreview);
        BuildEventSystem();

        var renderTarget = kiosk.AddComponent<PreviewRenderTarget>();
        Assign(renderTarget, "sourceCamera", previewCamera);
        AssignArray(renderTarget, "outputs", display2Image, panelPreview);

        var displays = new GameObject("Display Setup").AddComponent<MultiDisplaySetup>();
        Assign(displays, "panelCamera", panelCamera);
        Assign(displays, "previewCamera", display2Camera);
        Assign(displays, "panelScaler", panelScaler);

        EditorSceneManager.SaveScene(scene, ScenePath);
        var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Kiosk scene built at {ScenePath}");
    }

    // ---------------------------------------------------------------- 3D camera and Cinemachine rigs

    static Camera BuildPreviewCamera(out CinemachineBrain brain)
    {
        var camera = new GameObject("Preview Camera (Cinemachine Brain)").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.cullingMask = ~(1 << UILayer);
        camera.depth = -10f; // renders into the RenderTexture first; the UI cameras draw it afterwards
        camera.gameObject.AddComponent<AudioListener>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;

        brain = camera.gameObject.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 2.5f);
        brain.IgnoreTimeScale = true;
        camera.transform.SetPositionAndRotation(CityBuilder.IdleCamera,
            Quaternion.LookRotation(CityBuilder.IdleFocus - CityBuilder.IdleCamera));
        return camera;
    }

    static void BuildCameraRigs(KioskCameraDirector director, CinemachineBrain brain, InteriorLook look, BuildingInfo[] buildings)
    {
        var rigs = new GameObject("Camera Presets").transform;

        // Idle: a slow cinematic sweep across the front of the plaza (±35°, about 70 s per full swing)
        // that never swings round behind the buildings.
        var idle = PivotCamera(rigs, "Idle", CityBuilder.IdleFocus, CityBuilder.IdleCamera, CameraRigMotion.Mode.Sway, 35f, 0.85f);

        var buildingRigs = new BuildingRig[buildings.Length];
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            var exterior = PivotCamera(rigs, b.Name, b.Focus, b.CameraPosition, CameraRigMotion.Mode.Sway, 4f);
            buildingRigs[i] = new BuildingRig
            {
                name = b.Name,
                accent = b.Accent,
                exterior = exterior,
                // Entry and interior presets live inside each apartment so they move with it.
                apartments = b.Apartments.Select(a => new ApartmentRig
                {
                    label = a.Label,
                    details = a.Details,
                    entry = VirtualCamera(a.Entry.gameObject, 55f, 0.2f),
                    interior = VirtualCamera(a.Interior.gameObject, 72f, 0.05f),
                    lights = a.Lights,
                }).ToArray(),
            };
        }
        director.Configure(brain, look, idle, buildingRigs);
        EditorUtility.SetDirty(director);
    }

    /// <summary>A Cinemachine camera parented to a pivot at <paramref name="focus"/> that orbits or sways.</summary>
    static CinemachineCamera PivotCamera(Transform parent, string name, Vector3 focus, Vector3 position,
        CameraRigMotion.Mode motion, float degrees, float cyclesPerMinute = 2.5f)
    {
        var pivot = new GameObject($"{name} Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.position = focus;
        pivot.gameObject.AddComponent<CameraRigMotion>().Configure(motion, degrees, cyclesPerMinute);

        var go = new GameObject($"CM {name}");
        go.transform.SetParent(pivot, false);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
        return VirtualCamera(go, ExteriorFieldOfView, 0.5f);
    }

    static CinemachineCamera VirtualCamera(GameObject go, float fieldOfView, float nearClip)
    {
        var cam = go.AddComponent<CinemachineCamera>();
        var lens = LensSettings.Default;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = nearClip;
        lens.FarClipPlane = 2500f;
        cam.Lens = lens;
        cam.Priority = 0;
        return cam;
    }

    static Camera UICamera(string name, float height, int display, Color background)
    {
        // Renders only the UI layer, parked far below the city so it never sees the 3D scene.
        var camera = new GameObject(name).AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, height, 0f);
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        camera.cullingMask = 1 << UILayer;
        camera.targetDisplay = display;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        return camera;
    }

    // ---------------------------------------------------------------- Display 2: full-screen preview + caption

    static RawImage BuildDisplay2(Camera display2Camera, KioskStateManager state)
    {
        var canvas = CreateCanvas("Display 2 Canvas", display2Camera, 10f, out _);
        Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
        var image = Rect("Preview", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;

        var captionRoot = Rect("Caption", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var group = captionRoot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        var plate = Rect("Plate", captionRoot, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        plate.pivot = Vector2.zero;
        plate.anchoredPosition = new Vector2(70f, 70f);
        plate.sizeDelta = new Vector2(860f, 150f);
        var plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = RoundedSprite;
        plateImage.type = Image.Type.Sliced;
        plateImage.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);
        plateImage.raycastTarget = false;

        var subtitle = Label("Subtitle", plate, "", 26, MutedText, FontStyle.Bold, TextAnchor.LowerLeft);
        Stretch(subtitle.rectTransform, new Vector2(0f, 0.6f), new Vector2(1f, 0.9f), new Vector2(36f, 0f), new Vector2(-36f, 0f));
        var title = Label("Title", plate, "", 56, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
        Stretch(title.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.6f), new Vector2(36f, 0f), new Vector2(-36f, 0f));

        var caption = captionRoot.gameObject.AddComponent<PreviewCaption>();
        Assign(caption, "state", state);
        Assign(caption, "group", group);
        Assign(caption, "title", title);
        Assign(caption, "subtitle", subtitle);
        return image;
    }

    // ---------------------------------------------------------------- Display 1: touch control panel

    static CanvasScaler BuildControlPanel(Camera panelCamera, KioskStateManager state, DayNightController dayNight,
        InteriorLook look, BuildingInfo[] buildings, out RawImage livePreview)
    {
        var canvas = CreateCanvas("Display 1 Canvas", panelCamera, 10f, out var scaler);
        var root = (RectTransform)canvas.transform;

        var background = Rect("Background", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        background.gameObject.AddComponent<Image>().color = PanelBackground;

        var title = Label("Title", root, "BUILDING EXPLORER", 56, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(1600f, 80f));
        var breadcrumb = Label("Breadcrumb", root, "HOME", 26, MutedText, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(breadcrumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1600f, 40f));

        var idlePage = Page(root, "Idle Page");
        var buildingButtons = BuildIdlePage(idlePage.transform, buildings);

        var buildingPage = Page(root, "Building Page");
        BuildBuildingPage(buildingPage.transform, out var buildingTitle, out var buildingAccent, out var apartmentButtons,
            out var apartmentLabels, out var apartmentDetails, out var buildingBack);

        var apartmentPage = Page(root, "Apartment Page");
        BuildApartmentPage(apartmentPage.transform, state, look, out livePreview, out var apartmentTitle,
            out var apartmentInfo, out var apartmentBack, out var apartmentHome);

        var dayNightButton = CreateButton("Day Night Button", root, CardColor);
        Place((RectTransform)dayNightButton.transform, new Vector2(1f, 0f), new Vector2(-170f, 85f), new Vector2(260f, 100f));
        var dayNightLabel = Label("Label", dayNightButton.transform, "NIGHT", 34, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(dayNightLabel.rectTransform);

        var ui = canvas.gameObject.AddComponent<KioskPanelUI>();
        Assign(ui, "state", state);
        Assign(ui, "dayNight", dayNight);
        Assign(ui, "idlePage", idlePage);
        Assign(ui, "buildingPage", buildingPage);
        Assign(ui, "apartmentPage", apartmentPage);
        Assign(ui, "breadcrumb", breadcrumb);
        AssignArray(ui, "buildingButtons", buildingButtons);
        Assign(ui, "buildingTitle", buildingTitle);
        Assign(ui, "buildingAccent", buildingAccent);
        AssignArray(ui, "apartmentButtons", apartmentButtons);
        AssignArray(ui, "apartmentLabels", apartmentLabels);
        AssignArray(ui, "apartmentDetails", apartmentDetails);
        Assign(ui, "buildingBack", buildingBack);
        Assign(ui, "apartmentTitle", apartmentTitle);
        Assign(ui, "apartmentInfo", apartmentInfo);
        Assign(ui, "apartmentBack", apartmentBack);
        Assign(ui, "apartmentHome", apartmentHome);
        Assign(ui, "dayNightButton", dayNightButton);
        Assign(ui, "dayNightLabel", dayNightLabel);
        return scaler;
    }

    static CanvasGroup Page(Transform root, string name)
    {
        var page = Rect(name, root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<CanvasGroup>();
        page.alpha = name == "Idle Page" ? 1f : 0f;
        page.interactable = page.blocksRaycasts = name == "Idle Page";
        return page;
    }

    static Button[] BuildIdlePage(Transform page, BuildingInfo[] buildings)
    {
        var hint = Label("Hint", page, "Tap a building to explore it", 30, MutedText, FontStyle.Normal, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1600f, 50f));

        var cards = Rect("Building Buttons", page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        cards.anchoredPosition = new Vector2(0f, -40f);
        cards.sizeDelta = new Vector2(1680f, 520f);
        var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 48f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        var buttons = new Button[buildings.Length];
        for (int i = 0; i < buildings.Length; i++)
        {
            var button = CreateButton($"Building {i + 1} Button", cards, CardColor);
            var card = (RectTransform)button.transform;
            var number = Label("Number", card, $"{i + 1:00}", 150, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(number.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.95f));
            var name = Label("Name", card, buildings[i].Name.ToUpperInvariant(), 40, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(name.rectTransform, new Vector2(0f, 0.2f), new Vector2(1f, 0.42f));
            AccentBar(card, buildings[i].Accent);
            buttons[i] = button;
        }
        return buttons;
    }

    static void BuildBuildingPage(Transform page, out Text title, out Image accent, out Button[] apartments,
        out Text[] labels, out Text[] details, out Button back)
    {
        title = Label("Building Title", page, "", 72, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(1600f, 100f));
        var accentRect = Rect("Accent", page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        Place(accentRect, new Vector2(0.5f, 1f), new Vector2(0f, -295f), new Vector2(160f, 8f));
        accent = accentRect.gameObject.AddComponent<Image>();
        accent.sprite = RoundedSprite;
        accent.type = Image.Type.Sliced;
        accent.raycastTarget = false;
        var hint = Label("Hint", page, "Choose an apartment to step inside", 30, MutedText, FontStyle.Normal, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(1600f, 50f));

        apartments = new Button[2];
        labels = new Text[2];
        details = new Text[2];
        for (int i = 0; i < 2; i++)
        {
            apartments[i] = CreateButton($"Apartment {i + 1} Button", page, CardColor);
            var card = (RectTransform)apartments[i].transform;
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -380f : 380f, -110f), new Vector2(680f, 380f));
            labels[i] = Label("Label", card, i == 0 ? "1 BHK" : "2 BHK", 110, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(labels[i].rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 0.92f));
            details[i] = Label("Details", card, "", 28, MutedText, FontStyle.Normal, TextAnchor.UpperCenter);
            Stretch(details[i].rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.42f));
        }

        back = TextButton("Back Button", page, "‹  BACK", new Vector2(0f, 0f), new Vector2(200f, 85f), new Vector2(300f, 100f));
    }

    static void BuildApartmentPage(Transform page, KioskStateManager state, InteriorLook look, out RawImage preview,
        out Text title, out Text info, out Button back, out Button home)
    {
        // Live view of the 3D camera (the same RenderTexture Display 2 shows); drag it to look around.
        var frame = Rect("Preview Frame", page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Place(frame, new Vector2(0.5f, 0.5f), new Vector2(-250f, -60f), new Vector2(1216f, 691f));
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = RoundedSprite;
        frameImage.type = Image.Type.Sliced;
        frameImage.color = ActiveColor;
        frameImage.raycastTarget = false;

        var previewRect = Rect("Live Preview", frame, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        preview = previewRect.gameObject.AddComponent<RawImage>();
        preview.color = Color.white;
        var drag = previewRect.gameObject.AddComponent<LookDragArea>();
        Assign(drag, "look", look);
        Assign(drag, "state", state);

        var hintPlate = Rect("Swipe Hint", previewRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        Place(hintPlate, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(520f, 64f));
        var hintImage = hintPlate.gameObject.AddComponent<Image>();
        hintImage.sprite = RoundedSprite;
        hintImage.type = Image.Type.Sliced;
        hintImage.color = new Color(0f, 0f, 0f, 0.55f);
        hintImage.raycastTarget = false;
        var hint = Label("Text", hintPlate, "←  SWIPE TO LOOK AROUND  →", 26, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(hint.rectTransform);

        title = Label("Apartment Title", page, "", 64, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(640f, 210f), new Vector2(500f, 120f));
        info = Label("Apartment Info", page, "", 28, MutedText, FontStyle.Normal, TextAnchor.UpperLeft);
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        info.lineSpacing = 1.2f;
        // Below the two-line title ("2 BHK" + building name).
        Place(info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(640f, 35f), new Vector2(500f, 140f));

        back = TextButton("Back Button", page, "‹  BACK", new Vector2(0.5f, 0.5f), new Vector2(640f, -120f), new Vector2(500f, 110f));
        home = TextButton("Home Button", page, "HOME", new Vector2(0.5f, 0.5f), new Vector2(640f, -250f), new Vector2(500f, 110f));
    }

    static void BuildEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // ---------------------------------------------------------------- UI helpers

    static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static void AccentBar(RectTransform card, Color color)
    {
        var accent = Rect("Accent", card, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0f, 36f), new Vector2(0f, 50f));
        var image = accent.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    static Button TextButton(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var button = CreateButton(name, parent, CardColor);
        Place((RectTransform)button.transform, anchor, position, size);
        var label = Label("Label", button.transform, text, 38, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        return button;
    }

    static Canvas CreateCanvas(string name, Camera camera, float planeDistance, out CanvasScaler scaler)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = planeDistance;
        scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one);

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static Text Label(string name, Transform parent, string text, int size, Color color, FontStyle style, TextAnchor alignment)
    {
        var label = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Text>();
        label.font = UIFont;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    static Button CreateButton(string name, Transform parent, Color color)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        // Touch kiosks shouldn't keep a lingering "selected" state after a tap.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }

    static void Assign(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignArray(Object target, string property, params Object[] values)
    {
        var so = new SerializedObject(target);
        var array = so.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
