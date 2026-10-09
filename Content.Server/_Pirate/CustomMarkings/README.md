# CustomMarkings

Ported from [Wolfgate PR #179](https://github.com/VanguardControl/Wolfgate/pull/179), commit
`4cd4d80138eaec04e746b4e78bfff35d49b7e9e2`. The RobustToolbox revision is identical to Pirate
(`f47f6b02bf08bec9be36328208b728f9cff64aad`).

Pirate adaptations: the local humanoid rebuild event, two underwear layers and additional body sprites;
creator cards use the local stylesheet and fonts. PostgreSQL and SQLite have one migration each against
Pirate's existing model, and the editor strings live in `uk-UA` only. Source CVars, limits, shader IDs,
art format, frame timings and shipped icons are preserved.


Markings players draw themselves. The Custom markings card on the creator's Markings tab opens the player's
library: draw a new marking in the pixel editor or import a PNG, then wear up to four on a character. A marking is
four 32x32 facings and a placement, which sets its depth: behind the body, on the skin, on the hands and feet, over
the hair and ears or over everything. It only ever shows on the wearer's body and other markings, such as a tail or
ears, and two pixels around them.

A marking can be animated: up to eight frames, each shown for a time of its own. And it can erase parts of the body
under it, so that what is drawn takes the body's place instead of lying on top of it.

The library shows each marking on the character from all four sides, as it will look, with its name, placement and
icon actions. The editor keeps the drawing tools beside the canvas, the operations on the shown facing under them,
the facing tiles, the frame controls and the view options to its right and the colours below. Its colour picker
reads a drawn pixel or, where there is none, the body under it, which `CustomMarkingBodySampler` renders to a small
target and copies back. The body eraser is a tool of its own: left click erases the body, right click brings it
back, and while it is picked the facing operations work on what is erased. Both windows sit on a solid backdrop, as
the skin's glass panel is unreadable over the creator. The icons are drawn by
`Tools/_Pirate/CustomMarkings/gen_editor_icons.py`.

Art is stored once per drawing, under a hash of its pixels, and of its frame times and erase mask when it has them,
and sent to a client the first time it sees that hash. A character's profile lists what it wears as hash and
placement pairs, so a saved character keeps its markings when the library entry is changed or deleted; changing an
entry updates the character open in the creator.

Entry points: the server `CustomMarkingSystem` (library, saving, art requests, admin controls), the client
`CustomMarkingSystem` (fetches art, draws the layers), `CustomMarkingResources` (fetched art as in-memory RSIs),
`CustomMarkingLibraryWindow` and `CustomMarkingEditorWindow` (creator UI), `CustomMarkingArt`,
`CustomMarkingErase` and `CustomMarkingRules` (the art, its erase mask and the limits, shared).

Admin: saving new art is logged under the Identity log type with its hash. The Remove custom markings verb takes
them off a live character. The Block custom markings verb, or `custommarkingblock <hash> [true/false]`, blocks the
art itself: it comes off every body, is no longer sent to anyone and can't be saved again. A client that was told
art is blocked only asks for it again after reconnecting, so a lifted block shows from then. CVars:
`wf.custom_markings.enabled`, `wf.custom_markings.max_worn`, `wf.custom_markings.library_limit`,
`wf.custom_markings.max_frames` (1 allows only still markings), `wf.custom_markings.erase_body` (off, masks are
dropped from what is saved and those already saved are not applied), `wf.custom_markings.daily_art_limit` (most
new drawings a player may store in a day, 0 for no limit) and `wf.custom_markings.unused_art_days` (how long art
nothing uses is kept, 0 to keep it forever).

## How it fits together

- Clients upload raw pixels, never an image file, so the server parses nothing a player made. It checks the size,
  hashes the pixels and encodes the PNG itself. Frame times and the erase mask come as plain numbers and bits,
  checked the same way.
- The worn list rides on `HumanoidAppearanceComponent`, so anything that copies an appearance (cloning, polymorphs)
  copies the markings. Blocked art isn't filtered there; it is simply never sent.
- The client can't build an RSI in memory, so art is written into a mounted `MemoryContentRoot` as a `meta.json`
  and a PNG, and loads through the resource cache like a shipped sprite. The layers are rebuilt after every
  humanoid marking rebuild (`HumanoidMarkingsAppliedEvent`), each just under the first layer that draws over its
  placement.
- A marking only shows on the wearer's body, on the sprites of the markings the body shows (a tail, ears, hair)
  and on a two-pixel margin around them. Each of its pixels belongs to what it lies on, the margin to the nearest
  of them (`CustomMarkingSections`); a pixel on the body belongs to the body part, whatever else is drawn there.
  Every client cuts the art to the wearer's own outline as it draws (`CustomMarkingSystem.Limbs.cs`), so the limit
  holds whatever a player uploads. A hidden body part, such as a lost limb, takes its pixels along, and a marking
  that is hidden, a tail under a hardsuit say, is out of reach while it is. Art that already fits is drawn as it
  is. The editor only lets you draw within reach of the character shown, darkens the rest and outlines the edge.
  A severed limb doesn't carry the art, and neither does a transplant.
- An animated marking is one RSI state with a delay for each frame: its sheet holds every frame, laid out as an
  RSI lays them (`CustomMarkingArt.ToPng`), and the sprite layer runs through them like any animated sprite. A
  frame shows for 0.1 to 10 seconds; the floor keeps a marking from strobing. The times are stored beside the
  sheet, as an exported PNG can't hold them, and neither can it hold the erase mask.
- Erasing never changes the body's own sprites. Each layer the mask touches is drawn through a shader that drops
  the masked pixels (`erase.swsl`), fed by a layer that holds the mask and isn't drawn itself, the way clothing is
  fitted to a species with a displacement map (`CustomMarkingSystem.Erase.cs`). It applies to the body's parts, its
  eyes and the sprites of its markings; clothing is not a body layer, so nothing worn is ever erased. A layer that
  already has a shader of its own, other than unshaded, is left whole.
- Erasing must leave a body in sight, or a marking would make its wearer invisible. In each facing, at least half
  of the body's pixels have to stay or be drawn over solidly by the markings shown on it
  (`CustomMarkingErase.LeavesEnough`); otherwise nothing is erased at all. Like the reach limit, every client
  checks this for the wearer's own body as it draws, and the editor says so as it happens.
- The engine hands a mask to a layer's shader once per draw, set to the facing drawn, and a shader's settings only
  reach the screen when the frame's drawing is sent off. So one body drawn twice in one pass from two sides would
  show one side's mask on both, the same limit displacement maps have. The creator shows one side at a time, and
  the editor's canvases draw the layers themselves, each with a mask and shader of its own
  (`CustomMarkingEraseBrush`).
- A library holds 24 entries, but every redraw of an entry stores a new row of art and leaves the old one, since a
  saved character lists its art by hash and must keep it when the entry changes. Two things bound what that adds
  up to. A player may only store so many new drawings a day (`wf.custom_markings.daily_art_limit`); a drawing the
  server already holds adds no row and is always allowed. And as the server starts, with no round on and so no
  body wearing anything, it looks for art that no library holds and no saved character wears
  (`PurgeUnusedCustomMarkingArtAsync`): what it finds is noted with the time, and deleted by a later start once
  `wf.custom_markings.unused_art_days` have passed and it is still unused. Blocked art is never deleted, so a block
  holds. Until its time is out, art that has left every library can still be looked up and blocked by its hash. A
  character exported to a file and imported after that keeps its other markings but not the deleted art.
- Round replays don't record the art: a replay shows the bodies without their custom markings.
