# Geo Ad Carousel — how to edit it

The neon strip along the bottom of the game. This file is the tutorial: every section is a task,
and every step is a full menu path, so you can follow it without knowing Unity well.

---

## 1. Where everything lives

| What | Where |
|---|---|
| The prefab (all the visual values) | Project window > `Assets` > `GeoAds` > `Prefabs` > `GeoAdCarousel` |
| The one card to edit | Inside the prefab: `GeoAdCarousel` > `Viewport` > `Content` > `CardTemplate` |
| The script that fills cards with offers | `Assets` > `GeoAds` > `Scripts` > `GeoAdCarousel.cs` |
| The script that draws one card | `Assets` > `GeoAds` > `Scripts` > `GeoAdCard.cs` |
| The API address | Hierarchy > `GeoAds` > Inspector > `Geo Ad Manager` > **Api Url** |
| The builder | Menu bar > `Tools` > `GeoAds` > `Build Neon Carousel (Full-Width Bottom)` |
| Bundled backup data (used offline) | `Assets` > `GeoAds` > `Resources` > `GeoAds` > `mock_ads.json` |

**The one rule to remember:** `CardTemplate` is cloned once per offer, so *editing that one card
edits all 18 cards*. You never edit cards individually.

### Units (so the numbers below make sense)

The game canvas is set to **match width** with a 640 × 960 reference, which means the screen is
always **640 units wide** whatever device you run on. On a 405 × 720 portrait screen:

> 640 units = full screen width, so **1 unit ≈ 0.63 pixels**, and **61.3 units ≈ 39 px**.

The dock is 640 wide × 61.3 tall — full screen width, sitting on the bottom edge.

---

## 2. Adjust the height

1. Project window > `Assets` > `GeoAds` > `Prefabs` > double-click `GeoAdCarousel` (this opens Prefab Mode).
2. Hierarchy > `GeoAdCarousel` (select the top object).
3. Inspector > **Rect Transform** > set **Height** to whatever you want (61.3 is the default).
4. Press `Ctrl` + `Z` if you want to undo.

You do **not** have to move anything else. The card is anchored, not absolutely placed:

* `Title` is pinned to the **top** edge,
* `BottomRow` (discount chip, category, distance) is pinned to the **bottom** edge,
* `Claim` (the CTA button) stretches from top to bottom on the right,
* `Picture` stays centred on the left.

So when you raise the height the pieces spread apart instead of clipping. Font sizes do **not**
scale automatically — see section 5 if you make it much taller.

> Need to check it fits? Run `Tools` > `GeoAds` > `Build Neon Carousel (Full-Width Bottom)` —
> the Console prints `fit check PASS`, or names the child that sticks out.

---

## 3. Adjust the position

1. Project window > `Assets` > `GeoAds` > `Prefabs` > double-click `GeoAdCarousel`.
2. Hierarchy > select `GeoAdCarousel`.
3. Inspector > **Rect Transform** > click the square **anchor icon** (the small cross diagram at
   the top-left of the Rect Transform) to open Anchor Presets.
4. The default is **bottom-centre**: hold `Shift` and click the bottom-middle preset so position
   and pivot both jump to the bottom.
5. Inspector > **Rect Transform** > **Pos Y** is `0` (right on the bottom edge). Raise it to lift
   the strip above the bottom, e.g. `20`.

To change the width at the same time, hold `Shift` + `Alt` and pick **bottom-stretch**, then set
**Left** and **Right** to `0`.

---

## 4. Adjust the card width and paging

Cards are full screen width (640) with **0 spacing**, so exactly one card is visible and swiping
moves exactly one card — release snaps it into place.

1. Project window > `Assets` > `GeoAds` > `Prefabs` > double-click `GeoAdCarousel`.
2. Hierarchy > `GeoAdCarousel` > `Viewport` > `Content` > select `CardTemplate`.
3. Inspector > **Layout Element** > change **Preferred Width** (640 by default).

What happens:

* Keep it at **640** → one card fills the screen, no peeking.
* Make it **smaller** (e.g. 560) → the next card peeks in from the right edge, which looks good
  if you want a "there is more, swipe me" hint.
* Spacing between cards: Hierarchy > `Content` > Inspector > **Horizontal Layout Group** >
  **Spacing** (0 by default).
* Outer margin: same component > **Padding**.

Swiping speed: Hierarchy > `GeoAdCarousel` > Inspector > `Geo Ad Carousel` > **Snap Seconds**
(0.25 is the default; `0` = jump instantly instead of gliding).

---

## 5. Change colours, glow and fonts

Everything is a normal Inspector value — no code, no sprites to import.

**Neon palette used by the builder:** cyan `#00F0FF`, magenta `#FF2BD6`, purple `#8B5CFF`,
panel `#0A0620`, dim text `#C9C6E8`.

1. Project window > `Assets` > `GeoAds` > `Prefabs` > double-click `GeoAdCarousel`.
2. Hierarchy > `GeoAdCarousel` > `Viewport` > `Content` > `CardTemplate`.

Common edits on that object:

| Want to change | Select | Inspector field |
|---|---|---|
| Card border colour | `CardTemplate` | **Image** > **Color** |
| Border glow strength | `CardTemplate` | the two **Outline** components > **Effect Distance** and **Effect Color** alpha |
| Panel darkness | `CardTemplate` > `Panel` | **Image** > **Color** |
| Title font size | `CardTemplate` > `Title` | **TextMeshPro** > **Font Size** |
| Title glow | `CardTemplate` > `Title` | **Outline** component |
| Discount chip colour | `CardTemplate` > `BottomRow` > `Discount` | **Image** > **Color** |
| Category chip colour | `CardTemplate` > `BottomRow` > `Category` | **Image** > **Color** |
| CTA colour | `CardTemplate` > `Claim` | **Image** > **Color** (and its **Outline** for the glow) |
| Photo / logo size | `CardTemplate` > `Picture` (or `Logo`) | **Rect Transform** > Width / Height |
| Close button | `GeoAdCarousel` > `Close` | **Rect Transform** + **Image** |

**One colour is set by code, not the Inspector:** the card border switches colour per offer —
cyan for `affiliate` offers, magenta for `merchant_offer` offers — in
`GeoAdCard.cs` > `ApplyFrameTheme()`. If you want it always cyan, change that method to
`Color neon = NeonCyan;`.

---

## 6. Change the API endpoint, and how the offline fallback works

1. Hierarchy > select the `GeoAds` object (it lives in `Assets` > `DM_Scenes` > `Game`).
2. Inspector > `Geo Ad Manager`:
   * **Api Url** — the live feed, default `https://lootlio-one.vercel.app/api/mock`.
     A full `http(s)://…` address is fetched exactly as written. A plain base address such as
     `https://my-api.com` still uses the old `…/api/ads/nearby?lat=…&lng=…` form.
   * **Use Mock** — leave it **unticked** for the live feed. Tick it to always use the bundled file.
   * **Carousel** — which carousel to fill (the builder sets this for you).

**Fallback:** if the API is unreachable, times out or returns no offers, the manager automatically
loads `Resources/GeoAds/mock_ads.json` instead and writes a warning starting with `[GeoAds]`.
The strip never comes up empty.

Offers are cached for 5 minutes, so restarting a level does not refetch every time.

---

## 7. Re-run the builder

Use this after changing anything in the scripts, or if the prefab gets messy — it rebuilds the
prefab from scratch and re-wires the scene.

1. Open the scene first: Project window > `Assets` > `DM_Scenes` > double-click `Game`.
2. Menu bar > `Tools` > `GeoAds` > `Build Neon Carousel (Full-Width Bottom)`.
3. Window > General > Console and look for:
   * `[GeoAds] Build Neon Carousel: fit check PASS …`
   * `[GeoAds] Build Neon Carousel: feed check PASS - 18 offers parsed …`
   * `[GeoAds] Build Neon Carousel: done. …`
4. Save: `File` > `Save Project`.

It replaces the carousel currently in the open scene, keeps the same parent and bottom position,
points `Geo Ad Manager` at the API and saves the scene. It refuses to run in Play mode, and it
refuses to run if the open scene has no `GeoAds` object.

> It **overwrites** `Assets/GeoAds/Prefabs/GeoAdCarousel.prefab`. If you have hand-tuned that
> prefab heavily, duplicate it first: Project window > right-click `GeoAdCarousel.prefab` >
> `Duplicate`.

---

## 8. Testing it

1. Project window > `Assets` > `DM_Scenes` > double-click `Game`.
2. Toolbar > **Play**.
3. The neon strip appears at the bottom with the first offer fully visible — nothing clipped.
4. Drag left/right: one full card at a time, gliding and snapping to the next card (18 offers).
5. Tap a card: the coupon code is copied to the clipboard and the offer's page opens in the browser.
6. Tap **×** (top-right): the strip hides.
7. Turn Wi-Fi off and Play again: the strip still fills from `mock_ads.json`, with a `[GeoAds]`
   warning in the Console.

---

## 9. Troubleshooting

| Symptom | Fix |
|---|---|
| Nothing appears | Hierarchy > `GeoAds` > `Geo Ad Manager` > **Carousel** must reference `GeoAdCarousel`. Re-run the builder (section 7). |
| Console says `no Geo Ad Manager` when building | You are in the wrong scene — open `Assets/DM_Scenes/Game` first. |
| Only placeholder colours, no photos | Check the Console for `[GeoAds] picture download failed`. Offline is normal — the dark block is the offer's `placeholderColor`. |
| Card content is cut off | Run the builder and read the `fit check` line; it names the exact child that sticks out. |
| `feed check FAILED` | The API was unreachable from this machine, or it changed shape. The game still works via the offline fallback — but check the address in section 6. |
| Cards overlap or sit wrong after you resized things | Select `Content` > Inspector > **Rect Transform** > **Size Delta X** should be `0`, and `Content Size Fitter` > Horizontal Fit = `Preferred Size`. |
| Carousel is behind other UI | `GeoAdCarousel` has no Canvas of its own — it is drawn by its parent canvas. That is correct; do not add a Canvas. |