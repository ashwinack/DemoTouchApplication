# Building Reveal Kiosk — Setup Guide

A two-display Unity 6 kiosk running from one PC:

| Display | Hardware | Shows |
|---|---|---|
| **Display 1** | Lower touch screen | Control panel: building buttons → 1 BHK / 2 BHK → live 360° look-around preview |
| **Display 2** | Upper large screen | Full-screen 3D preview (idle orbit, building exteriors, apartment interiors) |

Flow: **Idle** (slow sweep across the front of all three buildings) → tap a building → **BuildingView** (camera flies to its exterior, panel offers 1 BHK / 2 BHK / Back) → tap an apartment → **ApartmentView** (camera flies through the windows into that apartment; swipe the panel's live preview to look around; Back / Home). After **60 s** without input the kiosk returns to Idle.

---

## 1. Quick start

1. Open the project in **Unity 6000.6** (URP, Input System, Cinemachine are already installed).
2. Run **Tools → Touch Demo → Build Scene**. This generates `Assets/Scenes/TouchDemo.unity` (city, buildings, interiors, cameras, UI) and makes it the first scene in Build Settings. It takes about 15 seconds.
3. Open two **Game** views (*Window → General → Game*). Set one to **Display 1** and the other to **Display 2**.
4. Press **Play** and click the Display 1 buttons, or use the keyboard:

| Key | Action |
|---|---|
| `1` `2` `3` | Idle: choose building · Building view: choose apartment (1 = 1 BHK, 2 = 2 BHK) |
| `Backspace` / `B` | Back |
| `Esc` / `H` | Home |
| `N` | Toggle day / night |

> Rebuilding regenerates the scene and everything in `Assets/Generated/`. Hand edits to the scene are lost, so tweak the generator scripts or stop using the generator (section 5).

---

## 2. Scene hierarchy

```
Sun                                  Light (sun by day, moon at night)
Post Processing - Day / - Night      Volume ×2 (weights cross-faded by DayNightController)
City
  Night Lights                       street lamps, floodlights, pool lights (night only)
  Ground
  Building 1 - Azure Tower           placeholder geometry + facade materials
    1 BHK Apartment                  interior room set (see below)
    2 BHK Apartment
  Building 2 - Terrace Center        procedural stepped building (see UseUsaBuildingForBuilding2)
    1 BHK Apartment / 2 BHK Apartment
  Building 3 - Helix Tower
    1 BHK Apartment / 2 BHK Apartment
  NYC Buildings                      buildings from the NYC pack, placed in city lots
  Streetscape / Context Buildings / Plaza & Park   (merged meshes)
Day Night                            DayNightController
Reflection Probe
Kiosk                                KioskStateManager, KioskCameraDirector, InteriorLook, PreviewRenderTarget
Preview Camera (Cinemachine Brain)   Camera + CinemachineBrain → renders into the RenderTexture
Camera Presets
  Idle Pivot            CameraRigMotion (Sway ±35°, front only)
    CM Idle             CinemachineCamera
  Azure Tower Pivot     CameraRigMotion (Sway)
    CM Azure Tower      CinemachineCamera
  Terrace Center Pivot / CM Terrace Center
  Helix Tower Pivot    / CM Helix Tower
Display 2 Camera                     UI-only camera, Target Display 2
Display 2 Canvas
  Preview                            RawImage (RenderTexture, full screen)
  Caption                            PreviewCaption
Display 1 Camera                     UI-only camera, Target Display 1
Display 1 Canvas                     KioskPanelUI
  Title, Breadcrumb
  Idle Page        (CanvasGroup)     Building 1/2/3 buttons
  Building Page    (CanvasGroup)     title, 1 BHK / 2 BHK buttons, Back
  Apartment Page   (CanvasGroup)     Preview Frame → Live Preview (RawImage + LookDragArea), title, info, Back, Home
  Day Night Button
EventSystem                          EventSystem + InputSystemUIInputModule
Display Setup                        MultiDisplaySetup
```

Each apartment contains:

```
2 BHK Apartment
  Interior Lights        point lights (enabled only while this apartment is shown)
  Floor, Ceiling, walls, window wall (mullions + glass), partitions with doorways
  Furniture groups       Sofa, TV Unit, Dining, Kitchen, Island, Beds, Wardrobe, Desk, …
  Entry Camera           CinemachineCamera, just outside the windows
  Interior Camera        CinemachineCamera, eye height; its rotation is the starting view
```

---

## 3. Scripts

### Runtime (`Assets/Scripts`)

| Script | Role |
|---|---|
| `Kiosk/KioskStateManager.cs` | Central state machine (`Idle`, `BuildingView`, `ApartmentView`). Public `SelectBuilding`, `SelectApartment`, `Back`, `Home`. Raises `StateChanged`. Watches all pointer, touch and keyboard input and returns to Idle after **Idle Timeout** (60 s). |
| `Kiosk/KioskCameraDirector.cs` | Holds the presets (`BuildingRig` → exterior camera + `ApartmentRig[]` with entry/interior cameras, labels, lights). Moves the camera by raising one `CinemachineCamera`'s priority, and sets the brain's ease-in-out blend time per hop. Apartments are routed exterior → **entry** → **interior**, and exits go back through the entry shot. Blend times are in the Inspector. |
| `Kiosk/InteriorLook.cs` | 360° look-around for the active interior camera: yaw is unlimited, pitch is clamped (default −35°…50°). Uses SmoothDamp and flick inertia, and resets to the authored view on each entry. |
| `Kiosk/LookDragArea.cs` | On the Display 1 live preview. Converts swipe/drag deltas (normalised by preview width) into `InteriorLook` input. |
| `Kiosk/PreviewRenderTarget.cs` | Creates a RenderTexture at Display 2's resolution and assigns it to the 3D camera, the Display 2 RawImage and the Display 1 live preview, so both show the same frame. |
| `Kiosk/KioskPanelUI.cs` | Display 1 UI: cross-fades the three pages, fills in titles and details from the director's rigs, wires the buttons, adds keyboard shortcuts, and drives the Day/Night button. |
| `Kiosk/CameraRigMotion.cs` | Rotates a camera pivot back and forth (**Sway**: the idle front sweep and the building shots), or all the way round (**Orbit**). |
| `Kiosk/HideWhileCameraInside.cs` | On apartments inside a real building model. Hides the model section the apartment sits in while the camera is inside, so its facade details don't block the window view. |
| `PreviewCaption.cs` | Display 2 caption that fades to describe the current view. |
| `MultiDisplaySetup.cs` | Activates Display 2 in builds. Falls back to side-by-side on one monitor if only one is connected (or **Force Split Screen** is ticked). |
| `DayNightController.cs` | Day/night cross-fade: sun/moon, sky, fog, post-processing, lit windows, street lights and floodlights. |

### Editor generators (`Assets/Editor`)

| Script | Role |
|---|---|
| `TouchDemoSceneBuilder.cs` | **Tools → Touch Demo → Build Scene**. Builds cameras, Cinemachine presets, both displays' UI, and wires every reference. |
| `City/CityBuilder.cs` | City, lighting, the three placeholder buildings, and building shot framing. |
| `City/ApartmentBuilder.cs` | Furnished 1 BHK (10 × 8 m) and 2 BHK (14 × 9 m) interiors, lights, and entry/interior camera presets. Each building has its own `InteriorTheme`. **Luxury** (Azure Tower): marble, navy slatted wall, velvet and brass, chandelier, black kitchen, 3.2 m ceiling. **Natural** (Terrace Center): oak floor and beams, terracotta wall, facing linen sofas, bookshelf, lots of plants, sage kitchen with open shelves. **Industrial** (Helix Tower): concrete floor, brick wall, glass-walled bedrooms, dark ceiling with ducts and track lights, leather sectional, steel shelving. The panel's apartment details show each theme's tagline and size. |
| `City/UsaTerraceBuilding.cs` | Assembles **Building 2** from the *USA Building With Terrace (HDRP, PBR)* prefabs, using the layout from the pack's sample scene. Removes the HDRP-only decals, and measures the facade so the apartments sit right behind it. If the pack is removed, the procedural Terrace Center is used instead. |
| `City/NycBuildings.cs` | Fills about 90% of the taller city lots with buildings from the *(HDRP) NYC-Like City Buildings Set*. Blocks that can take them are usually split into one or two large lots, because NYC buildings don't fit quarter-block lots. For each lot it picks a variant that fits and is close to the wanted height, strips the HDRP decals, and swaps the windows for the night-glow material. Lots where nothing fits stay procedural. The share is `nycShare` in `CityBuilder.ContextBlock`. |
| `City/PackTrees.cs` | Uses the *Trees_WorldSpace_FREE* tree for every tree in the city (streets, plaza, park, roof terraces). The pack's Built-in pipeline shaders don't work in URP, so URP/Lit copies are generated: 4 barks and one leaf material per atlas quadrant (5 green, plus 2 autumn colours for about 12% of trees). Trees are merged per material to keep draw calls low. Without the pack, the simple procedural trees are used. |
| `HdrpToUrpMaterialConverter.cs` | *Tools → Touch Demo → Convert Selected Folder's HDRP Materials to URP*. Converts HDRP/Lit materials in place: base, normal, mask (metallic/AO/smoothness), emission, alpha clip, transparency, double-sided. Originals are copied to `HDRP Material Backup/` in the project root. Decal materials are skipped. |
| `City/ProceduralTextures.cs`, `City/MeshLibrary.cs` | Generated textures and meshes. |

---

## 4. Configuring in the Inspector

**Kiosk → KioskStateManager**
- *Idle Timeout*: seconds of no input before returning to Idle (default 60).

**Kiosk → KioskCameraDirector**
- *Idle Camera*, *Buildings[]*: each has a *Name*, *Accent* colour (panel card bar), an *Exterior* camera, and *Apartments[]* (*Label*, *Details*, *Entry*, *Interior*, *Lights*).
- *Blend times*: To Idle 3 s, To Building 2.6 s, To Entry 2.2 s, Into Interior 1.6 s, Out Of Interior 1.2 s. All blends are Cinemachine ease-in-out.

**Moving a camera preset**: select the `CM …`, `Entry Camera` or `Interior Camera` object and move or rotate it in the Scene view. Tip: frame the Scene view the way you want, then use *GameObject → Align With View* (`Ctrl+Shift+F`). Building cameras orbit or sway around their **Pivot**, so put the pivot at the point the shot should look at.

**Kiosk → InteriorLook**
- *Degrees Per Preview Width* (default 200): how far a full-width swipe turns the view.
- *Pitch Limits* (−35, 50), *Smooth Time* (0.12 s), *Inertia Damping* (4).

**Camera Presets → … Pivot → CameraRigMotion**
- Idle: *Sway* ±35° at 0.85 cycles/min, a slow sweep across the front that never goes behind the buildings. Set *Mode* to *Orbit* for a full 360° circle instead.
- Buildings: *Sway* ±4° at 2.5 cycles/min.

**Kiosk → PreviewRenderTarget**
- *Display Index* 1 (Display 2). *Fallback Size* 1920×1080 is used in the Editor or when Display 2 isn't connected.

---

## 5. Replacing the placeholders with real models

**Building 2 can use a real model**, the *USA Building With Terrace* pack. It's switched off by default because the 187 m tower dwarfs the plaza. Set `UseUsaBuildingForBuilding2 = true` at the top of `City/CityBuilder.cs` and rebuild to use it. The plaza widens and the idle camera pulls back automatically. Asset Store packs made for **HDRP** render pink in this URP project. Convert their materials first: select the pack's `Materials` folder, then *Tools → Touch Demo → Convert Selected Folder's HDRP Materials to URP*. HDRP decals and HDRP-only components can't run in URP, so the generator strips them from its copy of the building.

> Packs from the same publisher can share file IDs. Importing *(HDRP) NYC-Like City Buildings Set* replaced the USA pack's `Sample.unity` with the NYC sample. That's why the USA building is rebuilt from its prefabs instead of being copied from its sample scene.

The generator is a fast way to get a working kiosk. For production:

1. Build the scene once, then **stop using Tools → Touch Demo → Build Scene**. It would overwrite your edits. Optionally copy `TouchDemo.unity` to a new scene name.
2. **Buildings**: drop your building model under `City/Building N - …`, then delete or disable the placeholder parts. Keep the `1 BHK Apartment` / `2 BHK Apartment` children, or replace them (step 3).
3. **Interiors**: place your interior model inside the building. Add two empty objects with a **CinemachineCamera** component (*Add Component → Cinemachine → Cinemachine Camera*): an **entry** shot just outside the apartment's windows, and an **interior** shot at eye height (~1.6 m) facing the starting view. Set the interior camera's Lens FOV to ~70 and Near Clip to ~0.05.
4. Put the interior's lights under one parent object.
5. On **Kiosk → KioskCameraDirector**, assign the new cameras and lights to the right building's *Apartments* entry. Edit *Label* and *Details*. The panel text updates automatically.
6. Leave every CinemachineCamera's **Priority** at 0. The director raises priority on the camera it wants live.
7. Make sure the camera's path from entry to interior isn't blocked by geometry you don't want to fly through. A glass curtain wall or open window works best.

### Building the scene by hand (without the generator)

1. *Window → Package Manager*: install **Cinemachine**. Input System and URP are already present.
2. Create **Preview Camera**: Camera + *Cinemachine Brain*. Culling Mask: everything except **UI**. Depth −10.
3. Create the presets: an **Idle Pivot** (with `CameraRigMotion`, Orbit) and a child *Cinemachine Camera*. For each building, a **Pivot** (Sway) and a child Cinemachine Camera. For each apartment, Entry and Interior Cinemachine Cameras. All priorities 0.
4. Create **Kiosk** with `KioskStateManager`, `KioskCameraDirector`, `InteriorLook` and `PreviewRenderTarget`. Assign the Brain, InteriorLook, cameras and lights in the director, and assign the director in the state manager.
5. Create **Display 2 Camera**: orthographic, Culling Mask UI only, Target Display = Display 2, Solid Color black, parked far from the city (e.g. y = −2000). Add **Display 2 Canvas**: Screen Space – Camera with that camera. Add a full-stretch **RawImage** ("Preview"), and optionally a caption with `PreviewCaption`.
6. Create **Display 1 Camera** the same way, with Target Display = Display 1 and y = −1000. Add **Display 1 Canvas**: Screen Space – Camera, Canvas Scaler *Scale With Screen Size* 1920×1080, Graphic Raycaster. Build three full-stretch child objects with **CanvasGroup**: Idle Page, Building Page and Apartment Page (contents as in section 2). On the Apartment Page, add a RawImage "Live Preview" with `LookDragArea` (assign InteriorLook and the state manager). Add `KioskPanelUI` to the canvas and assign every field.
7. On `PreviewRenderTarget`, set *Source Camera* = Preview Camera and *Outputs* = [Display 2 Preview RawImage, Display 1 Live Preview RawImage].
8. Add an **EventSystem** with **Input System UI Input Module**. Do not use the old Standalone Input Module, because the project uses the new Input System only.
9. Add `MultiDisplaySetup`: *Panel Camera* = Display 1 Camera, *Preview Camera* = Display 2 Camera, *Panel Scaler* = Display 1 Canvas' scaler.
10. Put all UI objects on the **UI** layer.

---

## 6. Deploying to the kiosk PC

1. **Windows display setup**: in *Settings → System → Display*, make the **touch screen display 1 (main display)** and the large screen display 2. Unity's `Display.displays[0]` is always the Windows main display.
2. **Touch mapping**: open *Control Panel → Tablet PC Settings → Setup → Touch input* and tap the lower screen when prompted, so touches land on the right display.
3. **Player Settings** (*Edit → Project Settings → Player → Resolution and Presentation*):
   - *Fullscreen Mode*: **Fullscreen Window** (needed for multi-display).
   - *Run In Background*: **on**. It's already enabled in this project. Without it the kiosk can freeze when Windows briefly moves focus.
4. **File → Build Profiles → Windows → Build**. On launch, `MultiDisplaySetup` activates Display 2. If only one monitor is attached, both views share it side by side, which is handy for testing on a laptop.
5. For unattended use, start the exe at login: Task Scheduler, or a shortcut in `shell:startup`.

---

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| Display 2 is black in a build | Check the second monitor is connected and in *Extend* mode, not *Duplicate*. |
| Touches act on the wrong screen | Redo the Windows touch-to-display mapping (section 6, step 2). |
| Buttons don't respond in the Editor | Click inside the **Display 1** Game view. The EventSystem only sees input on the display under the pointer. |
| Nothing animates in the Editor while another app has focus | That's Editor throttling. Click into Unity. |
| Pink/missing materials after editing generator code | Run **Tools → Touch Demo → Build Scene** again. |
| Interior looks dark | Check the apartment's *Lights* object is assigned in the director. The director only switches it on while that apartment is shown. |
