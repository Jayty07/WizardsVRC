# Story scenes

Talking to anyone in the world plays a **scene**: the screen opens in front of you, letterboxed, and a director films
whoever you clicked, anyone else in the scene and you. It is built the way Final Fantasy XIV stages its story scenes
(only the way: nothing of that game's is in here), because that grammar is what makes a conversation read as a story:

| In those scenes | Here |
|---|---|
| The scene plays for you alone; other players are not in it | Everything is local. The cast turn, gesture and walk on only on your side; the film shows your avatar and nobody else's |
| Your character stands in the scene and is in the shots | The film camera draws your own avatar (its mirror copy); wide shots, over-your-shoulder shots and your reaction shot all have you in them |
| Characters face whoever they speak to, and act: a nod, a shrug, a bow | Speakers turn to their listener and play one of thirteen emotes; listeners turn to the speaker |
| The camera cuts with the lines: wide to open, the speaker, over a shoulder, close for emphasis | The director's house style does exactly that, working each shot out from where everyone really stands |
| A dialogue window with a name plate; text types out; click to go on, or let it advance by itself | The talk window: name plate (blue ally, yellow neutral, red hostile), typed lines, click the picture or Space, an **Auto** button |
| Now and then you are asked something and pick an answer | `@choice`, up to three answers, and rows that play only after one of them |
| Fades, title cards, pauses, people arriving | `@fade`, `@card`, `@wait`, `@enter`, `@exit`, `@shot`, `@emote`, `@look` |
| People who belong elsewhere are simply there for the scene | Stand-ins: copies kept out of sight and brought on beside whoever you clicked |
| The interface is put away | The HUD shrinks away, name tags and quest markers are not filmed, and the world round the screen dims |

Nothing here needs writing by hand to work. Every existing conversation is already filmed this way from its plain
lines. The directions below are for the scenes that deserve more.

## What the player gets

- **The screen** opens level in front of you in VR, and where you are looking on a desktop (nearer, so it nearly fills
  the view). If something solid is in the way (the person you clicked, a counter) it opens nearer and smaller, so it
  looks the same size and stays clickable. The world behind it dims while the scene plays.
- **The talk window** lies across the bottom of the picture: the speaker's name on a plate, their initial in a
  medallion rimmed in their colour, and the line typed out at 45 letters a second. **Click the picture** (or press
  Space or Return) to finish the line; again to go on.
- **Auto** (on by default, the button under the picture): a line goes on by itself once it has been read (about
  1.4 s plus 35 ms a letter after it finishes typing), or, if it has a voice clip, when the clip ends.
- **Questions** replace the talk window with a prompt and up to three answers (click one, or press 1, 2 or 3). The
  camera turns to you while you choose.
- **Skip** ends the scene. Its quest step is still handed in, exactly as if it had played out.
- Walking off, respawning or being moved more than 4 m ends the scene the same way.

## How a scene is directed

**The cast.** Every NPC body is registered under its name when the world is built. A scene belongs to whoever you
clicked (its *own character*); every other speaker with a body joins the cast, and so does anyone a direction names.

- Someone standing within 14 m of the scene's own character, in the same area, plays their part **where they stand**.
- Anyone else is played by a **stand-in**: a copy of them with no name tag and no click box, kept switched off far
  below the world. When the scene opens, stand-ins are put on a ring round you, as far from you as the scene's own
  character is, fanned out either side of them: the first on your right of them, the next on your left, then further
  right, further left. Each mark has to be on solid ground, have room to stand and be in your sight; a mark that
  fails is passed over. If the real one is standing in view somewhere, they are hidden until the scene ends.
- At most eight bodies. A speaker with no body anywhere (a monster, a voice off stage) is heard, not seen.

**Acting.** Whoever speaks turns to whoever they are speaking to: the name in a `[to ...]` tag; else whoever they were
already speaking to; else whoever spoke before them; else you. Everyone else turns to the speaker. They then play
the line's emote. While a scene has them, bodies stand attentive (breathing, head still) instead of glancing about,
and when it ends they turn back the way they stood.

**The camera.** Six framings:

| Framing | What it is |
|---|---|
| `wide` | The speaker and their listener head to foot, from the side, a little round towards the listener so the speaker's face shows |
| `medium` | The speaker from the waist up, from in front and 27° to the side |
| `close` | Head and shoulders, on a longer lens |
| `shoulder` | Past the listener's shoulder at the speaker: the shoulder in one third of the picture, the speaker's face in the opposite one |
| `you` | Your own reaction shot, waist up |
| `group` | Everyone, from behind and beside you |

A line with no framing of its own is framed by the house style:

1. The scene opens `wide` (`group` when there are two or more of the cast), and 1.8 s later cuts in to `medium` on
   whoever is speaking. So even a one-line scene is seen from afar and from close by.
2. A new speaker gets `medium`.
3. Someone who goes on talking is cut between `shoulder`, `close`, `medium` and `wide`.
4. A short line ending in `!` (48 letters or fewer) comes in `close`.
5. A question put to you is shot over your shoulder.
6. The last line of a scene of four lines or more closes `wide`.
7. A line of your own (`You|...`) and a question are shot as `you`.

Every shot is worked out from where everyone stands at that moment, with faces placed 63% of the way up the picture
(clear of the talk window, with room for a hat). Before cutting to it the director checks that the camera is not
inside anything, that nothing solid lies between it and the faces it should see, and that no other body stands in
the way. If the shot fails it tries the other side, then both sides from closer in, then the nearest other framing
(a `medium` falls back to `close`, `shoulder`, `wide`), then the scene's old placed shot, and failing all of those it
stays where it is. Once it has crossed to the other side of a pair it stays there, so they do not swap places on
screen. Two who stand close together (within 1.5 to 2 m, by their size) are shot over the listener's shoulder
rather than side-on.

Each shot drifts slowly forward: about 10 cm a second on a wide shot, 4.5 on a medium, 2.5 on a close one.

**Scenes with nobody to film.** A boss speaking from its duel ring, a tower floor's greeting: their own character is a
monster, not an NPC body, so they play on their two placed shots as before, with the new window. `@wait`, `@fade`,
`@card` and `@choice` work there; the directions that need a cast do nothing. The same happens to any scene whose own
character is hidden or more than 14 m away when it starts.

## Writing a scene

Scenes are written wherever dialogue already is: an NPC's **Default Dialogue**, a quest's **Offer Dialogue**, a Talk
step's **Dialogue**, an encounter's **Intro Lines** (Assets/WizardsVRC/WorldData, or `WorldDefaults*.cs` for the game's
own). One row each. A row is a line of speech or a direction.

### Lines

| Row | Meaning |
|---|---|
| `words` | Spoken by the scene's own character |
| `Name|words` | Spoken by Name (their full name, as on their name tag) |
| `!Name|words` | The same, as an enemy: red plate, and an angry emote unless tagged |
| `You|words` | Your own line: the camera turns to you |
| `Name|[nod, close, to Ozza] words` | Tags, in square brackets before the words, separated by commas |

Tags:

- **Acting**: `still`, `talk`, `nod`, `shake`, `think`, `point`, `bow`, `surprise`, `cheer`, `laugh`, `angry`,
  `shrug`, `wave`, `sad`.
- **Framing**: `wide`, `medium`, `close`, `shoulder`, `you`, `group`.
- **Who it is said to**: `to Name` (a first name will do), `to you`, `to all`.

A line with no acting tag gets one picked from its words when the world is built, cautiously: a refusal shakes its
head, "thank you" bows, a greeting waves, "sorry" or "alas" hangs its head, "perhaps" or "they say" tilts it,
directions ("go to", "north", "follow the") point, a cry of 36 letters or fewer starts back, a question opens its
hands. Anything else alternates an open hand and a nod. Over the game's 358 lines that comes to 52% talk, 25% nod,
9% point and a few of each of the rest. When the pick is wrong, tag the line.

### Directions

| Row | Meaning |
|---|---|
| `@cast Name, Name, Name` | Who else is in the scene from the start, in the order they stand. Speakers join by themselves; this sets the order, and brings on those who never speak |
| `@wait 1.5` | A pause (1 s if no number). The talk window is put away |
| `@fade out 1` / `@fade in 1` | To black and back (0.6 s if no number) |
| `@card Words over black` | A title card: the picture goes dark under the words (`\n` for a second row). The next line or shot fades back in |
| `@shot close Name 2` | A cutaway: a framing, who (the scene's own character if nobody), seconds (1.6). `@shot wide Name with Other` frames the two of them |
| `@emote Name laugh 1.5` | Someone gestures without speaking. With a number the scene waits that long; without, it goes straight on, so several can gesture at once |
| `@look Name at Other` | Someone turns (`at you`; `@look Name away` turns them back the way they stood) |
| `@enter Name` / `@exit Name` | Someone walks on to their mark, or turns and walks off (1.1 s; a number changes it). Whoever a row brings on is not there when the scene opens |
| `@choice Prompt|Answer|Answer|Answer` | A question for the player: two or three answers |
| `1> row`, `2> row`, `3> row` | A row (line or direction) that plays only after that answer to the last question. Rows without a number go on for everyone |

In directions a first name, an NPC key or any one word of a name will do (`Ozza` is Madame Ozza Goldtongue), as long
as only one character answers to it. A direction that names nobody the world knows is reported when the world is
built, and does nothing.

Answers change only what is said and shown in the scene. They do not change quests. (`CutscenePlayer.LastAnswer()`
holds the last one, should a later system want it.)

### An example: the Moot

```
@cast Hesk, Pinch, Ozza, Tixi
@card The Deepwarden's hall\nThe first Moot in a hundred years
[group, bow] Bosses. Thank you for coming. Let's not draw wands until at least the second sentence.
Madame Ozza Goldtongue|[to Bruma] Fake gold on my Row, paid for in Syndicate silver. I want an apology and a refund. In that order.
"Big Pinch" Brassknuckle|[angry, to Ozza] Syndicate silver? It was Dominion paper, and your own collectors were taking it!
Grandmaw Hesk Rustclaw|[to Pinch] And my furnaces? I suppose the newts wrecked those.
Tixi Glimmerfang|[shrug, to Hesk] Ugh, listen to you. My pups were paid to steal my own lights. By spies. On my roof.
@shot group 1.6
Deepwarden Bruma Stonehum|[angry, close, to you] Enough. The wizard has walked every borough. Wizard, tell them what you found.
@choice What do you tell the bosses?|The Dominion is in every borough.|Someone has been buying all your gold.
1> @emote Pinch surprise
1> @shot close Pinch 1.4
2> @emote Ozza surprise
2> @shot close Ozza 1.4
Deepwarden Bruma Stonehum|[point, to all] Dominion scrip on the Row. Dominion drills in the Rustworks. ...
Deepwarden Bruma Stonehum|[close, to all] And every ounce of singing gold sold to one broker. Who weighs it for all of you.
@emote Ozza angry
@emote Hesk angry
@emote Pinch angry
@emote Tixi surprise
@shot group 2.4
```

The four bosses live in four other boroughs, so all four are stand-ins: Hesk on your right of the Deepwarden, Pinch
on your left, Ozza beyond Hesk, Tixi beyond Pinch. Each turns on whoever spoke last, the camera goes with them, and
the wizard is asked what they found.

Directed scenes in the game so far (all in World 3, world data v34): the **Moot**, the **Assayer** unmasked, the
**Silence** and the **Handshake** (quests 154 to 156), the gangs arriving at the **casino door** (160) and the
finale's **last scene** (161). An existing world-data folder takes the new rows only where a scene's lines are still
the old defaults; scenes you have edited are left alone.

### Voice clips

Clips are numbered by **spoken line**: `Audio/Voice/<prefix>_00.wav` is the first line spoken, `_01` the second.
Directions do not count, so adding them to a recorded scene does not move its clips. (Lines inside `1>` and `2>`
rows are numbered in the order they are written.)

## Checking a scene without Unity

`Tools/cutscene_model.py` mirrors the script reader, the emote picker and the director's staging and camera rules,
and reads the dialogue straight out of `WorldDefaults*.cs`:

```
python3 Tools/cutscene_model.py report               every scene parsed: warnings, how lines are acted and framed
python3 Tools/cutscene_model.py shots                where each framing puts the speaker's eyes, over a range of sizes and distances
python3 Tools/cutscene_model.py board 154 0 out.png  a storyboard of one quest step's scene (quest id, step, [answer])
python3 Tools/cutscene_model.py film 154 0 out.gif   the same scene played through, typed lines and all
```

Its stage is open ground and its figures are stand-in dolls: it shows the cuts, the framings, who faces whom and
what the screen looks like, not the game's art, and it does not model walls. Keep it in step with
`WizardsVRCSceneBuilder.Cutscenes.cs` and `CutscenePlayer.cs` when either changes.

## What it does not do

- **Your avatar does not act.** VRChat gives a world no way to animate a player's avatar, so you are in the shots as
  you stand.
- **Nobody walks a path.** `@enter` and `@exit` are a few steps on to a mark and off it; the cast otherwise stand
  where they are and turn.
- **Hands do not meet.** The Handshake is two bosses reaching for each other in a shot from the side.
- **Elbows are not animated** (each body's are built at their own bend), so every emote is made with the shoulders,
  head and spine, mostly the left arm: the right usually holds a staff.
- **Marks are found, not placed.** In a cramped room a stand-in may end at the first character's shoulder.

## Where it lives

| File | What |
|---|---|
| `Assets/WizardsVRC/Scripts/Cutscene.cs` | One scene: its beats (line, wait, fade, card, question, emote, look, enter, exit, cutaway) and its cast |
| `Assets/WizardsVRC/Scripts/CutscenePlayer.cs` | The director: the screen, the talk window, typing, Auto, questions, the cast's turns and emotes, stand-ins, every shot. Also plays spell films (`PlayFilm`) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Cutscenes.cs` | Reads rows into beats, picks emotes, registers bodies, gives each scene its cast and stand-ins, builds the screen |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Npcs.cs` | The bodies' Animator: the idles, the attentive scene idle and a clip for each emote |
| `Tools/cutscene_model.py` | The model above |
