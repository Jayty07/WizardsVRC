#!/usr/bin/env python3
"""
A model of the story-scene director (CUTSCENES.md), to check its rules outside Unity.

It mirrors, by hand, three pieces of the game (keep them in step when either side changes):
  - the script reader and the emote picker: Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Cutscenes.cs
    (ParseScene, ReadTags, ParseDirection, PickEmote);
  - the director's staging and camera: Assets/WizardsVRC/Scripts/CutscenePlayer.cs
    (Mark, Listener, SayFraming, Compose, Blocked, Find, Shoot);
  - the story's dialogue, read straight out of Assets/WizardsVRC/Editor/WorldData/WorldDefaults*.cs.

    python3 Tools/cutscene_model.py report            every scene in the story parsed: warnings, what the lines
                                                      are acted with, how each scene would be cut
    python3 Tools/cutscene_model.py shots             the camera's framings over a range of distances and sizes:
                                                      where the speaker's eyes sit in the picture, what is in the way
    python3 Tools/cutscene_model.py board 154 0 OUT.png   a storyboard of one quest step's scene (quest id, step),
                                                      drawn from the camera with stand-in figures
    python3 Tools/cutscene_model.py film 154 0 OUT.gif    the same scene played through as a GIF (typed lines and all)

The stage in the model is open ground: the first of the cast at the origin, you 2.4 m in front of them, everyone else
on their marks. It has no walls, so the director's line-of-sight tests against the world are not modelled, only its
test for bodies in the way.
"""
import glob
import math
import os
import re
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Assets", "WizardsVRC", "Editor", "WorldData")
FONT = os.path.join(ROOT, "Assets", "WizardsVRC", "Fonts", "PsxPixel.ttf")

# ---------------------------------------------------------------------------------------------- Cutscene.cs
SAY, WAIT, FADE_OUT, FADE_IN, CARD, CHOICE, EMOTE, LOOK, ENTER, EXIT, SHOT = range(11)
KIND_NAMES = ["say", "wait", "fade out", "fade in", "card", "choice", "emote", "look", "enter", "exit", "shot"]
AUTO, WIDE, MEDIUM, CLOSE, SHOULDER, YOU, GROUP = range(7)
FRAMING_NAMES = ["auto", "wide", "medium", "close", "shoulder", "you", "group"]
E_AUTO, E_NONE, E_TALK, E_NOD, E_SHAKE, E_THINK, E_POINT, E_BOW, E_SURPRISE, E_CHEER, E_LAUGH, E_ANGRY, E_SHRUG, E_WAVE, E_SAD = range(15)
EMOTE_LABELS = ["auto", "still", "talk", "nod", "shake", "think", "point", "bow", "surprise", "cheer", "laugh", "angry", "shrug", "wave", "sad"]
NOBODY, PLAYER = -1, -2

EMOTE_NAMES = {"still": E_NONE, "none": E_NONE, "talk": E_TALK, "nod": E_NOD, "shake": E_SHAKE, "think": E_THINK, "point": E_POINT,
               "bow": E_BOW, "surprise": E_SURPRISE, "surprised": E_SURPRISE, "cheer": E_CHEER, "laugh": E_LAUGH, "angry": E_ANGRY,
               "shrug": E_SHRUG, "wave": E_WAVE, "sad": E_SAD}
FRAMINGS = {"wide": WIDE, "medium": MEDIUM, "close": CLOSE, "shoulder": SHOULDER, "you": YOU, "group": GROUP}


class Beat:
    def __init__(self):
        self.kind = SAY
        self.speaker = ""
        self.hostile = False
        self.text = ""
        self.amount = 0.0
        self.branch = 0
        self.emote = E_AUTO
        self.framing = AUTO
        self.who = ""
        self.whom = ""
        self.picked = False   # the emote was picked from the words, not tagged


# ------------------------------------------------------------------ WizardsVRCSceneBuilder.Cutscenes.cs (the script)
def starts_any(s, *starts):
    return any(s.startswith(t) for t in starts)


def has_any(s, *bits):
    return any(t in s for t in bits)


def pick_emote(words, hostile, turn):
    text = words.strip()
    low = " " + text.lower() + " "
    head = text.lower().lstrip('. "\'')
    shout, ask, trails = text.endswith("!"), text.endswith("?"), text.endswith("...")
    if hostile:
        return E_ANGRY if shout or turn == 0 else (E_POINT if turn % 2 == 1 else E_TALK)
    if starts_any(head, "no,", "no.", "no!", "no deal", "never", "nay", "nope", "don't", "do not", "you can't", "can't"):
        return E_SHAKE
    if has_any(low, " thank you", " thanks", " my thanks", " grateful", " bless you", " in your debt", " honoured", " honored", " i owe you"):
        return E_BOW
    if has_any(low, " ha ha", " haha", " ha! ", " heh", " hah", " ho ho", " hee hee"):
        return E_LAUGH
    if starts_any(head, "welcome", "hello", "greetings", "well met", "hail", "good day", "good morning", "good evening", "howdy", "ahoy",
                  "farewell", "goodbye", "safe travels", "wizard!", "you came", "you're back", "back already", "ah, wizard", "ah. the wizard", "there you are"):
        return E_WAVE
    if has_any(low, " sorry", " alas", " i fear ", " i'm afraid", " mourn", " grief", " died", " went out", " going dark", " dark mine", " never thought"):
        return E_SAD
    if starts_any(head, "yes", "aye", "indeed", "very well", "good.", "good,", "right.", "right,", "agreed", "of course", "exactly",
                  "well done", "better.", "perfect", "much better", "not bad", "all four", "it's done", "done."):
        return E_NOD
    if starts_any(head, "hmm", "hm.", "hm,", "strange", "curious", "odd.", "odd,") or has_any(low, " i wonder", " perhaps", " strange ", " strange.",
                                                                                                  " curious ", " curious.", " puzzl", " riddle", " i've read", " they say", " folks say", " legend"):
        return E_THINK
    if starts_any(head, "find ", "follow ", "go ", "head ", "take the ", "look for ", "bring me ") or has_any(
            low, " go to ", " head to ", " head for ", " over there", " that way", " north", " south", " east", " west", " through the ", " past the ",
            " take the ", ". find ", " and find ", " go and find ", " look for ", " bring me ", " follow its ", " follow the ", " go on in", " step through"):
        return E_POINT
    if shout and len(text) <= 36:
        return E_SURPRISE
    if ask:
        return E_SHRUG
    if trails:
        return E_THINK
    return E_TALK if turn % 2 == 0 else E_NOD


def take_seconds(rest, fallback):
    rest = rest.strip()
    sp = rest.rfind(" ")
    last = rest if sp < 0 else rest[sp + 1:]
    try:
        v = float(last)
    except ValueError:
        return rest, fallback
    if not last:
        return rest, fallback
    return ("" if sp < 0 else rest[:sp].strip()), max(0.0, v)


def read_tags(scene, words, b, warn):
    words = words.lstrip()
    seen = False
    while words.startswith("["):
        end = words.find("]")
        if end < 0:
            break
        parts = words[1:end].split(",")
        tags = any(p.strip().lower().startswith("to ") or p.strip().lower() in EMOTE_NAMES or p.strip().lower() in FRAMINGS for p in parts)
        if not tags and not seen:
            break
        for part in parts:
            tag = part.strip()
            low = tag.lower()
            if not tag:
                continue
            if low.startswith("to "):
                b.whom = tag[3:].strip()
            elif low in EMOTE_NAMES:
                b.emote = EMOTE_NAMES[low]
            elif low in FRAMINGS:
                b.framing = FRAMINGS[low]
            else:
                warn(scene + ": unknown tag [" + tag + "]")
        seen = True
        words = words[end + 1:].lstrip()
    return words


def parse_direction(scene, row, b, warn):
    body = row[1:].strip()
    sp = body.find(" ")
    verb = (body if sp < 0 else body[:sp]).lower()
    rest = "" if sp < 0 else body[sp + 1:].strip()
    b.speaker = ""
    if verb == "wait":
        b.kind = WAIT
        rest, b.amount = take_seconds(rest, 1.0)
        return True
    if verb == "fade":
        low = rest.lower()
        fade_in = low.startswith("in")
        if fade_in or low.startswith("out"):
            b.kind = FADE_IN if fade_in else FADE_OUT
            rest, b.amount = take_seconds(rest[2 if fade_in else 3:], 0.0)
            return True
    elif verb == "card":
        if rest:
            b.kind = CARD
            b.text = rest.replace("\\n", "\n")
            return True
    elif verb in ("choice", "ask"):
        parts = []
        for part in rest.split("|"):
            if part.strip() or not parts:
                parts.append(part.strip())
        if len(parts) >= 2:
            if len(parts) > 4:
                warn(scene + ": a question takes three answers at most")
                parts = parts[:4]
            b.kind = CHOICE
            b.text = "|".join(parts)
            return True
    elif verb == "shot":
        sp2 = rest.find(" ")
        how = (rest if sp2 < 0 else rest[:sp2]).lower()
        if how in FRAMINGS:
            b.kind = SHOT
            b.framing = FRAMINGS[how]
            rest, b.amount = take_seconds("" if sp2 < 0 else rest[sp2 + 1:], 0.0)
            at = rest.lower().find(" with ")
            if at > 0:
                b.whom = rest[at + 6:].strip()
                rest = rest[:at].strip()
            b.who = "you" if b.framing == YOU else rest
            return True
    elif verb == "emote":
        rest, b.amount = take_seconds(rest, 0.0)
        sp2 = rest.rfind(" ")
        how = (rest if sp2 < 0 else rest[sp2 + 1:]).lower()
        if how in EMOTE_NAMES:
            b.kind = EMOTE
            b.emote = EMOTE_NAMES[how]
            b.who = "" if sp2 < 0 else rest[:sp2].strip()
            return True
    elif verb == "look":
        rest, b.amount = take_seconds(rest, 0.0)
        low = rest.lower()
        at = low.find(" at ")
        b.kind = LOOK
        if at > 0:
            b.who = rest[:at].strip()
            b.whom = rest[at + 4:].strip()
            return True
        if low.endswith(" away") or low.endswith(" back"):
            b.who = rest[:-5].strip()
            return True
    elif verb in ("enter", "exit"):
        rest, b.amount = take_seconds(rest, 0.0)
        if rest:
            b.kind = ENTER if verb == "enter" else EXIT
            b.who = rest
            return True
    warn(scene + ': can\'t read the direction "' + row + '"')
    return False


def parse_scene(scene, lead, rows, warn=lambda m: None):
    """Returns (beats, cast names from @cast rows)."""
    beats, cast = [], []
    last_speaker, turn = None, 0
    for raw in rows:
        if raw is None or not raw.strip():
            continue
        row = raw.strip()
        b = Beat()
        if len(row) > 2 and row[0] in "123" and row[1] == ">":
            b.branch = int(row[0])
            row = row[2:].lstrip()
        if row.lower().startswith("@cast "):
            cast += [n.strip() for n in row[6:].split(",") if n.strip()]
            continue
        if row.startswith("@"):
            if parse_direction(scene, row, b, warn):
                beats.append(b)
            continue
        who = lead or ""
        bar = row.find("|")
        if 0 < bar < 64:
            who = row[:bar].strip()
            row = row[bar + 1:].strip()
            if who.startswith("!"):
                b.hostile = True
                who = who[1:].strip()
        b.kind = SAY
        b.text = read_tags(scene, row, b, warn)
        b.speaker = who
        b.who = who
        turn = turn + 1 if who == last_speaker else 0
        last_speaker = who
        if b.emote == E_AUTO:
            b.emote = pick_emote(b.text, b.hostile, turn)
            b.picked = True
        beats.append(b)
    return beats, cast


def undirected(rows):
    """WorldDefaults.Undirected: a scene's rows with every direction, answer row and [tag] taken off."""
    plain = []
    for raw in rows:
        if raw is None or not raw.strip():
            continue
        row = raw.strip()
        if row.startswith("@") or (len(row) > 2 and row[0] in "123" and row[1] == ">"):
            continue
        who = ""
        bar = row.find("|")
        if 0 < bar < 64:
            who, row = row[:bar + 1], row[bar + 1:]
        words = row.lstrip()
        while words.startswith("["):
            end = words.find("]")
            if end < 0:
                break
            words = words[end + 1:].lstrip()
        plain.append(who + words)
    return plain


# ------------------------------------------------------------------ the story, out of WorldDefaults*.cs
def read_call(src, at):
    """The top-level arguments of the call whose '(' is at src[at]: a list of ('s', text) string literals and
    ('x', code) for anything else, and the index just past its ')'."""
    args, depth, i, cur, n = [], 0, at, "", len(src)
    while i < n:
        c = src[i]
        if c == '"':
            j, out = i + 1, []
            while j < n and src[j] != '"':
                if src[j] == "\\":
                    esc = src[j + 1]
                    out.append({"n": "\n", "t": "\t"}.get(esc, esc))
                    j += 2
                else:
                    out.append(src[j])
                    j += 1
            if depth == 1 and not cur.strip():
                args.append(("s", "".join(out)))
                cur = "\0"   # a literal fills this argument
            else:
                cur += src[i:j + 1]   # as written, escapes and all: a nested call is read again later
            i = j + 1
            continue
        if c == "/" and src[i:i + 2] == "//":
            i = src.find("\n", i)
            if i < 0:
                break
            continue
        if c in "([{":
            depth += 1
            if depth > 1:
                cur += c
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                if cur.strip() and cur != "\0":
                    args.append(("x", cur.strip()))
                return args, i + 1
            cur += c
        elif c == "," and depth == 1:
            if cur.strip() and cur != "\0":
                args.append(("x", cur.strip()))
            cur = ""
        elif depth >= 1:
            if cur == "\0" and c.strip():
                cur = c   # "a" + "b": rare; the first literal stands
            elif cur != "\0":
                cur += c
        i += 1
    return args, n


def load_story():
    """NPC names and colours by key, and every scene: {'name', 'lead_key', 'rows', 'quest', 'step'}."""
    npcs, scenes = {}, []
    files = sorted(glob.glob(os.path.join(DATA, "WorldDefaults*.cs")))
    sources = [open(f, encoding="utf-8").read() for f in files]
    colour = re.compile(r"new Color\(([\d.]+)f?, ([\d.]+)f?, ([\d.]+)f?\)")
    for src in sources:
        for m in re.finditer(r"\bNpc\(", src):
            args, _ = read_call(src, m.end() - 1)
            strings = [a[1] for a in args if a[0] == "s"]
            cols = [tuple(float(v) for v in colour.search(a[1]).groups()) for a in args if a[0] == "x" and colour.search(a[1])]
            if len(strings) < 3:
                continue
            key, name = strings[1], strings[2]
            npcs[key] = {"name": name, "robe": cols[0] if cols else (0.4, 0.4, 0.6), "hat": cols[1] if len(cols) > 1 else (0.2, 0.2, 0.35)}
            if len(strings) > 3:
                scenes.append({"name": key + " (default lines)", "lead_key": key, "rows": strings[3:], "quest": -1, "step": -1})
        for m in re.finditer(r'key = "(\w+)", displayName = "([^"]+)"(?:, robeColor = (new Color\([^)]*\)))?(?:, hatColor = (new Color\([^)]*\)))?', src):
            robe = colour.search(m.group(3)).groups() if m.group(3) else None
            hat = colour.search(m.group(4)).groups() if m.group(4) else None
            npcs.setdefault(m.group(1), {"name": m.group(2), "robe": tuple(float(v) for v in robe) if robe else (0.4, 0.4, 0.6),
                                         "hat": tuple(float(v) for v in hat) if hat else (0.2, 0.2, 0.35)})
    for src in sources:
        # Quests: Q(id, "title", ...) with Talk(...) steps among its arguments, in order.
        for m in re.finditer(r"\bQ\((\d+),", src):
            args, _ = read_call(src, m.end() - len(m.group(1)) - 2)
            quest = int(m.group(1))
            step = 0
            for kind, code in args:
                if kind != "x":
                    continue
                call = re.match(r"(Talk|Fight|Point)\(", code)
                if not call:
                    continue
                if call.group(1) == "Talk":
                    targs, _ = read_call(code, call.end() - 1)
                    strings = [a[1] for a in targs if a[0] == "s"]
                    if len(strings) > 3:
                        scenes.append({"name": "Quest %d step %d (%s)" % (quest, step, strings[0]), "lead_key": strings[0], "rows": strings[3:],
                                       "quest": quest, "step": step})
                step += 1
    for s in scenes:
        s["lead"] = npcs.get(s["lead_key"], {}).get("name", s["lead_key"])
    return npcs, scenes


# ------------------------------------------------------------------ CutscenePlayer.cs (staging and camera)
UP = np.array([0.0, 1.0, 0.0])
EYE_LINE = 0.26   # CutscenePlayer.EyeLine


def flat(v):
    return math.hypot(v[0], v[2])


def norm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


class Actor:
    def __init__(self, name, mark, face, robe, hat, stand_in=False):
        self.name = name
        self.mark = np.array(mark, float)
        self.face = face
        self.robe = robe
        self.hat = hat
        self.stand_in = stand_in
        self.on = True
        self.look = None        # the point they are turned to
        self.emote = E_NONE
        self.emote_at = -9.0


class Director:
    """The staging and camera of one scene on open ground."""

    def __init__(self, cast, player_feet, player_eye=1.6):
        self.cast = cast
        self.feet = np.array(player_feet, float)
        self.head = self.feet + UP * player_eye
        self.size = min(max(player_eye / 1.6, 0.45), 1.7)
        d = self.feet - cast[0].mark
        d[1] = 0
        self.axis = norm(d)
        self.right = np.cross(UP, self.axis)
        self.side = 1.0
        self.says = 0
        self.cycle = 0
        self.last_speaker = NOBODY
        self.last_listener = PLAYER
        self.shot = None       # (framing, subject, other)
        self.cam = None        # (pos, look, fov)
        self.notes = []
        for a in cast:
            a.look = self.feet.copy()

    # -- who
    def on(self, a):
        return 0 <= a < len(self.cast) and self.cast[a].on

    def who(self, a):
        return a if a == PLAYER or self.on(a) else NOBODY

    def lead(self):
        for i, a in enumerate(self.cast):
            if a.on:
                return i
        return NOBODY

    def stand_of(self, w):
        return self.cast[w].mark if w >= 0 else self.feet

    def face_of(self, w):
        return self.cast[w].mark + UP * self.cast[w].face if w >= 0 else self.head

    def size_of(self, w):
        return min(max(self.cast[w].face / 1.6, 0.55), 1.6) if w >= 0 else self.size

    # -- acting
    def address(self, who, listener):
        for i, a in enumerate(self.cast):
            if not a.on:
                continue
            if i == who:
                if listener != NOBODY:
                    a.look = self.stand_of(listener).copy()
            elif who != NOBODY:
                a.look = self.stand_of(who).copy()

    def facing(self, a):
        """Who 'a' is turned to: whoever stands where they are looking (you, when it's nobody on stage)."""
        if not (0 <= a < len(self.cast)) or self.cast[a].look is None:
            return PLAYER
        for k, other in enumerate(self.cast):
            if k != a and other.on and flat(self.cast[a].look - other.mark) < 0.3:
                return k
        return PLAYER

    def listener(self, target, who):
        to = self.who(target)
        if to != NOBODY and to != who:
            return to
        if who == self.last_speaker and self.last_listener != who and (self.last_listener == PLAYER or self.on(self.last_listener)):
            return self.last_listener
        if self.last_speaker != who and (self.last_speaker == PLAYER or self.on(self.last_speaker)):
            return self.last_speaker
        return self.lead() if who == PLAYER else PLAYER

    # -- the camera
    def say_framing(self, framing, who, listener, text, last_line):
        if framing != AUTO:
            return framing
        if who == PLAYER:
            return YOU
        on_stage = sum(1 for a in self.cast if a.on)
        if who == NOBODY:
            return GROUP if on_stage >= 2 else WIDE
        if self.says == 0:
            self.cycle = 0
            return GROUP if on_stage >= 2 else WIDE
        if who != self.last_speaker:
            self.cycle = 1
            framing = MEDIUM
        else:
            k = self.cycle % 5
            self.cycle += 1
            framing = SHOULDER if k == 1 else (CLOSE if k == 2 else (WIDE if k == 4 else MEDIUM))
        if framing != CLOSE and len(text) <= 48 and text.endswith("!"):
            framing = CLOSE
        elif framing == MEDIUM and listener == PLAYER and text.endswith("?"):
            framing = SHOULDER
        if self.says >= 3 and (self.shot is None or self.shot[0] != WIDE) and last_line:
            framing = WIDE
        return framing

    def compose(self, framing, subject, other, flip, near):
        fs, fo = self.face_of(subject), self.face_of(other)
        s, so = self.size_of(subject), self.size_of(other)
        floor = min(self.stand_of(subject)[1], self.feet[1])
        u = fo - fs
        u[1] = 0
        gap = np.linalg.norm(u)
        if gap < 0.2:
            u = -self.axis if subject == PLAYER else self.axis.copy()
            gap = 0.2
        else:
            u = u / gap
        r = np.cross(UP, u)
        sgn = (1.0 if np.dot(r, self.right) * self.side >= 0 else -1.0) * flip
        if framing in (MEDIUM, CLOSE, YOU) and other != NOBODY and gap < 1.5 * s:
            framing = SHOULDER   # shoulder to shoulder: past the other's shoulder, not side-on
        self.try_framing = framing
        if framing == GROUP:
            pts = [self.head] + [self.face_of(i) for i in range(len(self.cast)) if self.on(i)]
            centre = sum(pts) / len(pts)
            reach = max(flat(p - centre) for p in pts)
            back = max(3.1, (reach + 1.1) / 0.72) * near
            fov = 50.0
            pos = centre + (self.axis * 0.82 + self.right * (0.57 * self.side * flip)) * back + UP * (0.35 + 0.1 * back)
            look = centre - UP * 0.8
        elif framing == WIDE:
            big = max(s, so)
            mid = (fs + fo) * 0.5
            back = max(3.3 * big, gap * 0.62 + 1.6) * near
            fov = 50.0
            pos = mid + (r * (0.94 * sgn) + u * 0.34) * back - UP * (0.25 * big)
            look = mid - UP * (0.85 * big)
        elif framing == SHOULDER:
            if other == NOBODY:
                return None
            fov = 40.0
            pos = fo + u * (0.8 * so * near) + r * (0.55 * so * sgn) + UP * (0.12 * so - 0.25 * max(0.0, fs[1] - fo[1]))
            reach = np.linalg.norm(pos - fs)
            look = fs - r * (0.17 * reach * sgn) - UP * (EYE_LINE * reach * math.tan(math.radians(fov * 0.5)))
        else:
            close = framing == CLOSE
            fov = 34.0 if close else 40.0
            back = (1.37 if close else 1.98) * s * near
            angle = math.radians(20.0 if close else 27.0)
            room = gap - 0.5
            if back * math.cos(angle) > room:
                back = max(back * 0.75, room / math.cos(angle))
                if back * math.cos(angle) > room:
                    angle = math.acos(min(max(room / back, 0.2), 1.0))
            pos = fs + (u * math.cos(angle) + r * (math.sin(angle) * sgn)) * back - UP * (0.03 * s)
            look = fs - UP * (EYE_LINE * back * math.tan(math.radians(fov * 0.5)))
        if pos[1] < floor + 0.35:
            pos[1] = floor + 0.35
        return pos, look, fov

    def blocked(self, cam, face, a, b, cast=True):
        dx, dz = face[0] - cam[0], face[2] - cam[2]
        span = dx * dx + dz * dz
        if span < 1e-4:
            return False
        for k in range(-1, len(self.cast) if cast else 0):
            w = PLAYER if k < 0 else k
            if w == a or w == b or (k >= 0 and not self.on(k)):
                continue
            foot, size = self.stand_of(w), self.size_of(w)
            t = min(max(((foot[0] - cam[0]) * dx + (foot[2] - cam[2]) * dz) / span, 0.0), 1.0)
            if t > 0.97:
                continue
            px, pz = cam[0] + dx * t - foot[0], cam[2] + dz * t - foot[2]
            if px * px + pz * pz > 0.34 * 0.34 * size * size:
                continue
            y = cam[1] + (face[1] - cam[1]) * t
            if foot[1] - 0.1 < y < self.face_of(w)[1] + 0.3 * size:
                return True
        return False

    def clear(self, shot, subject, other):
        pos, framing = shot[0], self.try_framing
        if framing == GROUP:
            return not any(self.on(i) and self.blocked(pos, self.face_of(i), i, i, False) for i in range(len(self.cast)))
        pair = framing in (WIDE, SHOULDER)
        if self.blocked(pos, self.face_of(subject), subject, other if pair else subject):
            return False
        if not pair or other == NOBODY:
            return True
        return framing != WIDE or not self.blocked(pos, self.face_of(other), subject, other)

    def find(self, framing, subject, other):
        for v in range(4):
            flip = 1.0 if v % 2 == 0 else -1.0
            shot = self.compose(framing, subject, other, flip, 1.0 if v < 2 else 0.72)
            if shot is None or not self.clear(shot, subject, other):
                continue
            if flip < 0:
                self.side = -self.side
            return shot
        return None

    @staticmethod
    def next_best(framing, k):
        table = {WIDE: (MEDIUM, SHOULDER, CLOSE), GROUP: (WIDE, MEDIUM, CLOSE), CLOSE: (MEDIUM, SHOULDER, WIDE),
                 SHOULDER: (MEDIUM, CLOSE, WIDE), YOU: (CLOSE, WIDE, GROUP)}
        return table.get(framing, (CLOSE, SHOULDER, WIDE))[k]

    def shoot(self, framing, subject, other):
        if subject == NOBODY:
            subject = self.lead()
        if subject == NOBODY:
            subject = PLAYER
        if other == subject or (other >= 0 and not self.on(other)) or other == NOBODY:
            other = self.lead() if subject == PLAYER else PLAYER
        if self.shot == (framing, subject, other):
            return
        shot, used = self.find(framing, subject, other), framing
        k = 0
        while shot is None and k < 3:
            used = self.next_best(framing, k)
            shot = self.find(used, subject, other)
            k += 1
        if shot is None:
            self.notes.append("no clear view for %s of %s" % (FRAMING_NAMES[framing], self.name_of(subject)))
            if self.cam is not None:
                return
            shot = self.compose(framing, subject, other, 1.0, 1.0) or self.compose(MEDIUM, subject, other, 1.0, 1.0)
        elif used != framing:
            self.notes.append("%s of %s had no clear view: %s instead" % (FRAMING_NAMES[framing], self.name_of(subject), FRAMING_NAMES[used]))
        self.shot = (framing, subject, other)
        self.cam = shot

    def name_of(self, w):
        return "you" if w == PLAYER else ("nobody" if w == NOBODY else self.cast[w].name)


def marks(lead_mark, feet, count):
    """CutscenePlayer.Mark on open ground: stand-ins on a ring round you, fanned out either side of the lead."""
    axis = norm(np.array([feet[0] - lead_mark[0], 0.0, feet[2] - lead_mark[2]]))
    d = min(max(flat(np.array(lead_mark) - np.array(feet)), 2.2), 6.0)
    step = min(max(2.0 * math.degrees(math.asin(min(0.65 / d, 1.0))), 14.0), 40.0)
    out = []
    for s in range(count):
        turn = math.radians((1.0 if s % 2 == 0 else -1.0) * step * (s // 2 + 1))
        # Quaternion.Euler(0, turn, 0) * v: a positive turn swings a direction to its right, seen from above.
        v = -axis
        out.append(np.array(feet, float) + np.array([v[0] * math.cos(turn) + v[2] * math.sin(turn), 0.0, -v[0] * math.sin(turn) + v[2] * math.cos(turn)]) * d)
    return out


def find_name(name, npcs, cast_names):
    """WizardsVRCSceneBuilder.FindBody / CastIndex, loosely: a full name, an NPC key, or a word of a name."""
    low = name.strip().lower()
    if low in ("you", "player", "wizard", "the wizard", "all", "everyone"):
        return PLAYER
    for key, n in npcs.items():
        if n["name"].lower() == low or key.lower() == low:
            return n["name"]
    hits = [n["name"] for n in npcs.values() if low in [w.lower() for w in re.split(r'[ ,"]', n["name"])]]
    if len(set(hits)) == 1:
        return hits[0]
    for c in cast_names:
        if low in c.lower():
            return c
    return None


def stage(scene, npcs, warn=lambda m: None, gap=2.4, face=1.75, player_eye=1.6):
    """A scene's beats with its cast resolved and standing on open ground."""
    beats, order = parse_scene(scene["name"], scene["lead"], scene["rows"], warn)
    by_name = {n["name"]: n for n in npcs.values()}
    names = [scene["lead"]]

    def index(name, loose):
        if not name.strip():
            return NOBODY
        found = find_name(name, npcs, names) if loose else (name if name in by_name else (PLAYER if name == "You" else None))
        if found == PLAYER:
            return PLAYER
        if found is None:
            if loose:
                warn(scene["name"] + ': nobody called "' + name + '"')
            return NOBODY
        if found not in names:
            names.append(found)
        return names.index(found)

    for name in order:
        index(name, True)
    for b in beats:
        b.actor = index(b.who, b.kind != SAY) if (b.kind == SAY or b.who) else NOBODY
        b.target = index(b.whom, True)
        if b.kind == EMOTE and not b.who:
            b.actor = 0
    feet = (0.0, 0.0, gap)
    spots = marks((0.0, 0.0, 0.0), feet, len(names) - 1)
    cast = []
    for i, name in enumerate(names):
        look = by_name.get(name, {"robe": (0.4, 0.4, 0.6), "hat": (0.2, 0.2, 0.35)})
        cast.append(Actor(name, (0, 0, 0) if i == 0 else spots[i - 1], face, look["robe"], look["hat"], i > 0))
    for b in beats:
        if b.kind == ENTER and b.actor > 0:
            cast[b.actor].on = False
    return beats, cast, Director(cast, feet, player_eye)


def play(beats, cast, director, answer=1):
    """Runs a scene: yields (beat, camera, notes) for every beat that holds the screen."""
    d = director
    current = 0
    for i, b in enumerate(beats):
        if b.branch and b.branch != current:
            continue
        who = d.who(b.actor)
        d.notes = []
        if b.kind == SAY:
            listener = d.listener(b.target, who)
            d.address(who, listener)
            if who >= 0 and not (d.says == 0 and who == 0 and b.emote in (E_AUTO, E_TALK)):
                cast[who].emote = b.emote
            last = not any(x.kind == SAY and (x.branch == 0 or x.branch == current) for x in beats[i + 1:])
            framing = d.say_framing(b.framing, who, listener, b.text, last)
            if framing == YOU:
                d.shoot(framing, PLAYER, listener if who == PLAYER else who)
            else:
                d.shoot(framing, who, listener)
            first_cam, first_notes = d.cam, list(d.notes)
            # Establish, then go in: the opening line starts wide and cuts to its speaker 1.8 s later.
            cut = None
            if d.says == 0 and who >= 0 and b.framing == AUTO:
                d.notes = []
                d.shoot(MEDIUM, who, listener)
                d.cycle = 1
                cut = (1.8, d.cam, list(d.notes))
            d.last_speaker, d.last_listener = who, listener
            d.says += 1
            yield b, first_cam, first_notes, FRAMING_NAMES[framing] + (" -> " + d.name_of(listener)), cut
        elif b.kind == CHOICE:
            d.address(PLAYER, d.last_speaker)
            d.shoot(YOU if b.framing == AUTO else b.framing, PLAYER, d.last_speaker)
            current = answer
            yield b, d.cam, list(d.notes), "you", None
        elif b.kind == SHOT:
            framing = MEDIUM if b.framing == AUTO else b.framing
            if b.actor == PLAYER or framing == YOU:
                d.shoot(framing, PLAYER, d.last_speaker)
            else:
                pair = d.who(b.target)
                d.shoot(framing, who, pair if pair != NOBODY else d.facing(who))
            yield b, d.cam, list(d.notes), FRAMING_NAMES[framing], None
        elif b.kind == EMOTE:
            if who >= 0:
                cast[who].emote = b.emote
                if b.framing != AUTO:
                    d.shoot(b.framing, who, d.facing(who))
                if b.amount > 0:
                    yield b, d.cam, list(d.notes), "emote", None
        elif b.kind == LOOK:
            if who >= 0:
                at = d.who(b.target)
                cast[who].look = d.stand_of(at).copy() if at != NOBODY else cast[who].mark + np.array([0.0, 0.0, 3.0])
        elif b.kind == ENTER:
            if 0 <= b.actor < len(cast) and not cast[b.actor].on:
                cast[b.actor].on = True
                cast[b.actor].look = d.feet.copy()
                d.shoot(MEDIUM if b.framing == AUTO else b.framing, b.actor, PLAYER)
                yield b, d.cam, list(d.notes), "enter", None
        elif b.kind == EXIT:
            if who >= 0:
                cast[who].on = False
                yield b, d.cam, list(d.notes), "exit", None
        elif b.kind in (CARD, WAIT, FADE_OUT, FADE_IN):
            yield b, d.cam, [], KIND_NAMES[b.kind], None


# ------------------------------------------------------------------ reports
def project(cam, p, w=1.0, h=1.0):
    """A point in the picture: (x, y) as shares of its width and height from the bottom left, and its depth."""
    pos, look, fov = cam
    f = norm(look - pos)
    r = norm(np.cross(UP, f))
    u = np.cross(f, r)
    v = p - pos
    z = float(np.dot(v, f))
    if z <= 0.05:
        return None
    k = 1.0 / math.tan(math.radians(fov * 0.5))
    x = np.dot(v, r) / z * k / (16.0 / 9.0)
    y = np.dot(v, u) / z * k
    return (0.5 + x * 0.5) * w, (0.5 + y * 0.5) * h, z


# The picture's clear band: above the talk window (the bottom 215 of 900) and below the top letterbox bar (64 of 900).
WINDOW_TOP, BAR_BOTTOM = 215.0 / 900.0, 1.0 - 64.0 / 900.0


def report_shots():
    print("Where the camera puts the speaker's eyes (share of the picture's height; the clear band is %.2f-%.2f),"
          % (WINDOW_TOP, BAR_BOTTOM))
    print("with you 1.6 m to the eyes and the speaker's face at 'face' m, 'gap' m apart.\n")
    print("%-9s %5s %5s | %6s %6s %6s | %s" % ("framing", "gap", "face", "eyes x", "eyes y", "dist", "notes"))
    worst = 0
    for framing in (WIDE, MEDIUM, CLOSE, SHOULDER, YOU, GROUP):
        for gap in (1.0, 1.5, 2.4, 3.5):
            for face in (1.0, 1.3, 1.6, 2.0):
                cast = [Actor("Speaker", (0, 0, 0), face, (0.5, 0.4, 0.6), (0.2, 0.2, 0.3))]
                d = Director(cast, (0, 0, gap))
                subject, other = (PLAYER, 0) if framing == YOU else (0, PLAYER)
                d.shoot(framing, subject, other)
                eyes = project(d.cam, d.face_of(subject))
                notes = list(d.notes)
                if eyes is None:
                    notes.append("SPEAKER BEHIND THE CAMERA")
                    worst += 1
                    print("%-9s %5.1f %5.1f | %6s %6s %6s | %s" % (FRAMING_NAMES[framing], gap, face, "-", "-", "-", "; ".join(notes)))
                    continue
                if not (0.04 < eyes[0] < 0.96):
                    notes.append("EYES OFF THE SIDE")
                    worst += 1
                if not (WINDOW_TOP + 0.02 < eyes[1] < BAR_BOTTOM - 0.02):
                    notes.append("EYES OUTSIDE THE CLEAR BAND")
                    worst += 1
                if framing in (MEDIUM, CLOSE, YOU):
                    # Nobody else's head may sit in front of the face.
                    o = project(d.cam, d.face_of(other))
                    if o is not None and o[2] < eyes[2] and abs(o[0] - eyes[0]) < 0.08 and abs(o[1] - eyes[1]) < 0.14:
                        notes.append("LISTENER'S HEAD IN FRONT")
                        worst += 1
                print("%-9s %5.1f %5.1f | %6.2f %6.2f %6.2f | %s" % (FRAMING_NAMES[framing], gap, face, eyes[0], eyes[1], eyes[2], "; ".join(notes)))
    print("\n%d framing problems." % worst)
    return worst


def report_story():
    npcs, scenes = load_story()
    warnings = []
    counts, picked = {}, {}
    cuts = {}
    lines = directed = 0
    for sc in scenes:
        beats, cast, d = stage(sc, npcs, warnings.append)
        if any(b.kind != SAY or not b.picked or b.framing != AUTO or b.whom for b in beats):
            directed += 1
        for b in beats:
            if b.kind != SAY:
                continue
            lines += 1
            counts[b.emote] = counts.get(b.emote, 0) + 1
            if b.picked:
                picked.setdefault(b.emote, []).append(b.text)
        for b, cam, notes, how, cut in play(beats, cast, d):
            if b.kind == SAY:
                cuts[how.split(" ")[0]] = cuts.get(how.split(" ")[0], 0) + 1
                if cut:
                    cuts["medium"] = cuts.get("medium", 0) + 1
            for n in notes + (cut[2] if cut else []):
                warnings.append(sc["name"] + ": " + n)
    print("%d scenes (%d with directions or tags of their own), %d spoken lines, %d characters.\n" % (len(scenes), directed, lines, len(npcs)))
    print("How the lines are acted (picked from their words unless tagged):")
    for e in sorted(counts, key=lambda k: -counts[k]):
        print("  %-9s %4d  %4.1f%%" % (EMOTE_LABELS[e], counts[e], 100.0 * counts[e] / lines))
    print("\nHow the lines are framed (by the director's house style unless tagged):")
    for k in sorted(cuts, key=lambda k: -cuts[k]):
        print("  %-9s %4d  %4.1f%%" % (k, cuts[k], 100.0 * cuts[k] / max(1, sum(cuts.values()))))
    print("\nA few of each pick, to read against the rule:")
    for e in sorted(picked, key=lambda k: -len(picked[k])):
        print("  " + EMOTE_LABELS[e] + ":")
        for t in picked[e][:: max(1, len(picked[e]) // 6)][:6]:
            print("      " + t[:110])
    print("\n%d warnings." % len(warnings))
    for w in warnings:
        print("  " + w)
    return len(warnings)


# ------------------------------------------------------------------ pictures
def _font(size):
    from PIL import ImageFont
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default()


def figure_parts(actor, t, is_player=False, feet=None, eye=1.6):
    """A body as shaded balls (centre, radius, colour): a robe down to the ground, a big head with eyes and a nose
    (so you can see which way it faces), a pointed hat, and its arms, held however its emote holds them at time t.
    Everything is in proportion to how high its face is: the game's bodies have their hips at about half that."""
    if is_player:
        base, face_y, robe, hat = np.array(feet, float), eye, (0.22, 0.35, 0.7), (0.15, 0.2, 0.45)
        look, emote, since = actor, E_NONE, 9.0
    else:
        base, face_y, robe, hat, look = actor.mark, actor.face, actor.robe, actor.hat, actor.look
        emote, since = actor.emote, t - actor.emote_at
    k = face_y / 2.04   # NpcModel: hips 1.05, neck 0.74 above them, the face 0.25 above that
    d = np.array([look[0] - base[0], 0.0, look[2] - base[2]])
    fwd = norm(d) if np.linalg.norm(d) > 1e-6 else np.array([0.0, 0.0, 1.0])
    left = np.cross(fwd, UP)
    lean, nod = 0.0, 0.0
    wave = math.sin(min(since, 2.0) * 9.0) if since < 2.0 else 0.0
    arm_fwd, arm_out, arm_r_out, arm_r_fwd = 0.0, 8.0, 8.0, 0.0
    if 0.0 <= since < 2.2:
        if emote == E_TALK:
            arm_fwd, arm_out = 48.0, 16.0
        elif emote == E_POINT:
            arm_fwd = 82.0
        elif emote == E_WAVE:
            arm_out = 118.0 + wave * 10.0
        elif emote == E_CHEER:
            arm_fwd, arm_r_fwd = 145.0 + wave * 12.0, 28.0
        elif emote == E_SHRUG:
            arm_out, arm_r_out, arm_fwd, arm_r_fwd = 34.0, 30.0, 20.0, 16.0
        elif emote == E_SURPRISE:
            arm_out, arm_r_out, lean = 38.0, 26.0, -8.0
        elif emote == E_ANGRY:
            arm_fwd, lean = 58.0 + wave * 8.0, 10.0
        elif emote == E_BOW:
            lean, nod, arm_fwd, arm_r_fwd = 32.0, 0.1, 26.0, 26.0
        elif emote == E_SAD:
            lean, nod = 7.0, 0.12
        elif emote == E_NOD:
            nod = 0.07 * max(0.0, wave)
        elif emote == E_THINK:
            arm_fwd = 32.0
        elif emote == E_LAUGH:
            lean = -6.0
    parts = []
    hip = base + UP * 1.05 * k
    tilt = math.radians(lean)

    def spine(h):   # a point h metres (at full size) up the spine from the hips, leaning forward from them
        return hip + (UP * math.cos(tilt) + fwd * math.sin(tilt)) * h * k

    for i in range(7):   # the robe, hem to hips
        f = i / 6.0
        parts.append((base + UP * (0.18 + 0.87 * f) * k, (0.44 - 0.17 * f) * k, robe))
    for i in range(4):   # hips to shoulders
        f = i / 3.0
        parts.append((spine(0.1 + 0.5 * f), (0.28 - 0.04 * f) * k, robe))
    head = spine(0.99) + fwd * nod * k - UP * nod * 0.4 * k
    skin = (0.86, 0.68, 0.52)
    parts.append((head, 0.25 * k, skin))
    for side in (1.0, -1.0):   # eyes
        parts.append((head + fwd * 0.215 * k + left * side * 0.09 * k + UP * 0.04 * k, 0.04 * k, (0.08, 0.06, 0.08)))
    parts.append((head + fwd * 0.245 * k - UP * 0.03 * k, 0.05 * k, (0.74, 0.54, 0.42)))
    for i in range(6):   # a pointed hat on a brim
        f = i / 5.0
        parts.append((head + UP * (0.2 + 0.5 * f) * k - fwd * 0.06 * f * k, (0.27 - 0.24 * f) * k, hat))
    for side, out_deg, fwd_deg in ((1.0, arm_out, arm_fwd), (-1.0, arm_r_out, arm_r_fwd)):
        root = spine(0.6) + left * side * 0.3 * k
        a, o = math.radians(fwd_deg), math.radians(out_deg)
        down = (-UP * math.cos(a) + fwd * math.sin(a)) * math.cos(o) + left * side * math.sin(o)
        for i in range(5):
            f = (i + 1) / 5.0
            parts.append((root + down * 0.74 * f * k, (0.09 - 0.012 * f) * k, robe if i < 4 else skin))
    return parts


def render(cam, cast, director, t=0.0, w=480, h=270, show_player=True):
    """The camera's view: ground, sky and everyone on stage as shaded balls, with a depth buffer."""
    pos, look, fov = cam
    f = norm(look - pos)
    r = norm(np.cross(UP, f))
    u = np.cross(f, r)
    k = math.tan(math.radians(fov * 0.5))
    ys, xs = np.mgrid[0:h, 0:w]
    px = (xs + 0.5 - w / 2) / (h / 2) * k
    py = -(ys + 0.5 - h / 2) / (h / 2) * k
    dirs = f[None, None, :] + px[..., None] * r[None, None, :] + py[..., None] * u[None, None, :]
    dirs /= np.linalg.norm(dirs, axis=2, keepdims=True)
    img = np.zeros((h, w, 3))
    # The sky: dusk under the world, a little lighter at the horizon.
    up = np.clip(dirs[..., 1], 0, 1)
    img[:] = np.array([0.16, 0.13, 0.24])[None, None, :] * (1 - up[..., None] * 0.7) + np.array([0.1, 0.07, 0.12])[None, None, :] * up[..., None] * 0.7
    depth = np.full((h, w), 1e9)
    # The ground: flagstones a metre across, fading with distance; a soft shadow under everyone.
    down = dirs[..., 1] < -1e-4
    tg = np.where(down, -pos[1] / np.where(down, dirs[..., 1], -1.0), 1e9)
    gx = pos[0] + dirs[..., 0] * tg
    gz = pos[2] + dirs[..., 2] * tg
    tile = ((np.floor(gx) + np.floor(gz)) % 2) * 0.05
    edge = np.minimum(np.abs(gx - np.round(gx)), np.abs(gz - np.round(gz)))
    ground = np.stack([0.33 + tile, 0.27 + tile, 0.25 + tile], axis=2) * np.where(edge < 0.03, 0.8, 1.0)[..., None]
    shade = np.ones((h, w))
    bodies = [(a.mark, a.face / 2.04 * 1.3) for a in cast if a.on]
    if show_player:
        bodies.append((director.feet, director.size))
    for mark, size in bodies:
        dd = np.hypot(gx - mark[0], gz - mark[2])
        shade = np.minimum(shade, 0.45 + 0.55 * np.clip(dd / (0.5 * size), 0, 1))
    fog = np.clip(tg / 30.0, 0, 1)[..., None]
    ground = ground * shade[..., None] * (1 - fog) + np.array([0.16, 0.13, 0.24])[None, None, :] * fog
    img = np.where((down & (tg < 60))[..., None], ground, img)
    depth = np.where(down & (tg < 60), tg, depth)

    light = norm(np.array([-0.4, 0.8, -0.45]))
    parts = []
    for a in cast:
        if a.on:
            parts += figure_parts(a, t)
    if show_player:
        lead = director.lead()
        parts += figure_parts(director.stand_of(lead) if lead >= 0 else director.feet - director.axis, t, True, director.feet, director.head[1] - director.feet[1])
    for centre, radius, colour in parts:
        v = centre - pos
        z = float(np.dot(v, f))
        if z < 0.06:
            continue
        cx = w / 2 + np.dot(v, r) / z / k * (h / 2)
        cy = h / 2 - np.dot(v, u) / z / k * (h / 2)
        pr = radius / z / k * (h / 2)
        x0, x1 = int(max(0, cx - pr - 1)), int(min(w, cx + pr + 2))
        y0, y1 = int(max(0, cy - pr - 1)), int(min(h, cy + pr + 2))
        if x0 >= x1 or y0 >= y1:
            continue
        sx = (xs[y0:y1, x0:x1] + 0.5 - cx) / pr
        sy = (ys[y0:y1, x0:x1] + 0.5 - cy) / pr
        rr = sx * sx + sy * sy
        inside = rr <= 1.0
        nz = np.sqrt(np.clip(1.0 - rr, 0, 1))
        zs = z - nz * radius
        nearer = inside & (zs < depth[y0:y1, x0:x1])
        normal = sx[..., None] * r + (-sy)[..., None] * u - nz[..., None] * f
        lit = 0.42 + 0.58 * np.clip(normal @ light, 0, 1)
        rim = np.where(rr > 0.72, 0.72, 1.0)   # a dark edge, like the game's outlines
        shaded = np.array(colour)[None, None, :] * (lit * rim)[..., None]
        img[y0:y1, x0:x1] = np.where(nearer[..., None], shaded, img[y0:y1, x0:x1])
        depth[y0:y1, x0:x1] = np.where(nearer, zs, depth[y0:y1, x0:x1])
    return np.clip(img, 0, 1)


def wrap(draw, text, font, width):
    out, line = [], ""
    for word in text.split(" "):
        trial = (line + " " + word).strip()
        if draw.textlength(trial, font=font) <= width or not line:
            line = trial
        else:
            out.append(line)
            line = word
    if line:
        out.append(line)
    return out


STANCE_COLOURS = [(128, 191, 255), (255, 217, 89), (255, 115, 102)]


def dress(img, beat, shown=None, bars=1.0, fade=0.0, w=480, h=270):
    """The screen over the picture: letterbox bars, the talk window with its name plate, a card, a question."""
    from PIL import Image, ImageDraw
    im = Image.fromarray((img * 255).astype(np.uint8)).convert("RGB")
    if fade > 0:
        im = Image.blend(im, Image.new("RGB", im.size, (0, 0, 0)), fade)
    draw = ImageDraw.Draw(im, "RGBA")
    bar = int(round(64 / 900 * h * bars))
    draw.rectangle([0, 0, w, bar], fill=(0, 0, 0, 255))
    draw.rectangle([0, h - bar, w, h], fill=(0, 0, 0, 255))
    s = h / 900.0
    small, body = _font(max(8, int(30 * s))), _font(max(8, int(33 * s)))
    if beat is None:
        return im
    if beat.kind == CARD:
        big = _font(max(10, int(48 * s)))
        lines = []
        for part in beat.text.split("\n"):
            lines += wrap(draw, part, big, w * 0.75)
        y = h / 2 - len(lines) * 9
        for line in lines:
            draw.text((w / 2 - draw.textlength(line, font=big) / 2, y), line, font=big, fill=(255, 255, 255, 255))
            y += int(60 * s)
        return im
    if beat.kind == CHOICE:
        parts = beat.text.split("|")
        pw, ph = 1100 * s, 310 * s
        x0, y0 = w / 2 - pw / 2, h - (310 + 5) * s
        draw.rectangle([x0, y0, x0 + pw, y0 + ph], fill=(30, 22, 48, 242), outline=(226, 172, 58, 255), width=2)
        draw.text((w / 2 - draw.textlength(parts[0], font=body) / 2, y0 + 14 * s), parts[0], font=body, fill=(255, 230, 140, 255))
        for k, answer in enumerate(parts[1:4]):
            by = y0 + 76 * s + k * 74 * s
            draw.rectangle([w / 2 - 500 * s, by, w / 2 + 500 * s, by + 64 * s], fill=(76, 86, 138, 255), outline=(226, 172, 58, 255))
            draw.text((w / 2 - draw.textlength(answer, font=small) / 2, by + 17 * s), answer, font=small, fill=(255, 255, 255, 255))
        return im
    if beat.kind != SAY:
        return im
    # The talk window: 1400 x 215 centred 290 below the middle of a 1600 x 1000 screen whose picture is 900 tall.
    x0, x1 = w / 2 - 700 * s, w / 2 + 700 * s
    y0, y1 = h - (215 + 5) * s, h - 5 * s
    draw.rectangle([x0, y0, x1, y1], fill=(30, 22, 48, 237), outline=(226, 172, 58, 255), width=2)
    stance = 2 if beat.hostile else (0 if beat.speaker.startswith(("Professor", "Headmaster")) or beat.speaker == "You" else 1)
    colour = STANCE_COLOURS[stance] + (255,)
    if beat.speaker:
        nx0, ny0 = x0 + 55 * s, y0 - 41 * s
        draw.rectangle([nx0, ny0, nx0 + 520 * s, ny0 + 62 * s], fill=(52, 34, 26, 255), outline=(150, 100, 40, 255))
        draw.text((nx0 + 260 * s - draw.textlength(beat.speaker, font=small) / 2, ny0 + 15 * s), beat.speaker, font=small, fill=colour)
        cx, cy, rad = x0 + 100 * s, (y0 + y1) / 2 + 4 * s, 70 * s
        draw.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], fill=(64, 54, 83, 255), outline=colour, width=2)
        name = beat.speaker.lstrip('"\' ')
        initial = _font(max(10, int(74 * s)))
        draw.text((cx - draw.textlength(name[:1].upper(), font=initial) / 2, cy - 36 * s), name[:1].upper(), font=initial, fill=colour)
    text = beat.text if shown is None else beat.text[:shown]
    lines = wrap(draw, beat.text, body, 1140 * s)
    left, y, used = x0 + 225 * s, y0 + 36 * s, 0
    for line in lines:
        part = line if used + len(line) <= len(text) else line[:max(0, len(text) - used)]
        draw.text((left, y), part, font=body, fill=(255, 255, 255, 255))
        used += len(line) + 1
        y += int(40 * s)
    if shown is None or shown >= len(beat.text):
        draw.polygon([(x1 - 48 * s, y1 - 40 * s), (x1 - 36 * s, y1 - 28 * s), (x1 - 48 * s, y1 - 16 * s), (x1 - 60 * s, y1 - 28 * s)], fill=(255, 209, 89, 255))
    return im


def find_scene(scenes, quest, step):
    for s in scenes:
        if s["quest"] == quest and s["step"] == step:
            return s
    raise SystemExit("No Talk step %d in quest %d." % (step, quest))


def storyboard(quest, step, out, answer=1):
    from PIL import Image, ImageDraw
    npcs, scenes = load_story()
    sc = find_scene(scenes, quest, step)
    warnings = []
    beats, cast, d = stage(sc, npcs, warnings.append)
    panels = []
    t = 0.0
    for b, cam, notes, how, cut in play(beats, cast, d, answer):
        for a in cast:
            if a.emote != E_NONE and a.emote_at < 0:
                a.emote_at = t - 0.6   # mid-gesture
        label = KIND_NAMES[b.kind] if b.kind != SAY else how
        if b.kind == SAY:
            label += "   (" + EMOTE_LABELS[b.emote] + ")"
        elif b.kind == SHOT:
            label = "cutaway: " + how + (" on " + d.name_of(b.actor) if b.actor != NOBODY else "")
        elif b.kind == ENTER:
            label = d.name_of(b.actor) + " walks on"
        dark = b.kind in (CARD, FADE_OUT)
        for shot, text, extra in [(cam, label, notes)] + ([(cut[1], "... then in: medium", cut[2])] if cut else []):
            img = np.zeros((270, 480, 3)) if shot is None else render(shot, cast, d, t)
            panels.append((dress(img, b, fade=1.0 if dark else 0.0), text, extra))
        for a in cast:
            a.emote, a.emote_at = E_NONE, -9.0
        t += 3.0
    cols = 3
    rows = (len(panels) + cols - 1) // cols
    pad, cap = 14, 22
    sheet = Image.new("RGB", (cols * (480 + pad) + pad, rows * (270 + cap + pad) + pad + 30), (18, 15, 26))
    draw = ImageDraw.Draw(sheet)
    draw.text((pad, 8), sc["name"] + ": how the director cuts it (answer %d)" % answer, font=_font(16), fill=(255, 220, 130))
    for i, (im, label, notes) in enumerate(panels):
        x = pad + (i % cols) * (480 + pad)
        y = 30 + pad + (i // cols) * (270 + cap + pad)
        sheet.paste(im, (x, y))
        draw.text((x, y + 273), "%d. %s%s" % (i + 1, label, ("   ! " + "; ".join(notes)) if notes else ""), font=_font(12), fill=(190, 185, 210))
    sheet.save(out)
    print("%s: %d panels -> %s" % (sc["name"], len(panels), out))
    for wmsg in warnings:
        print("  warning: " + wmsg)


def film(quest, step, out, answer=1, fps=10):
    """The scene played through: typed lines, timed holds, the letterbox sliding in."""
    npcs, scenes = load_story()
    sc = find_scene(scenes, quest, step)
    beats, cast, d = stage(sc, npcs)
    frames = []
    t = 0.0
    first = True
    for b, cam, notes, how, cut in play(beats, cast, d, answer):
        if b.kind == SAY:
            hold = len(b.text) / 45.0 + 1.4 + len(b.text) * 0.035
        elif b.kind == CARD:
            hold = b.amount or 2.4 + len(b.text) * 0.05
        elif b.kind == CHOICE:
            hold = 2.5
        elif b.kind == WAIT:
            hold = b.amount or 1.0
        elif b.kind in (FADE_OUT, FADE_IN):
            hold = b.amount or 0.6
        else:
            hold = b.amount or (1.6 if b.kind == SHOT else 1.1)
        for a in cast:
            if a.emote != E_NONE and a.emote_at < 0:
                a.emote_at = t
        n = max(1, int(round(hold * fps)))
        for i in range(n):
            lt = i / fps
            # The camera's slow push in (and the opening line's cut from the wide shot to its speaker).
            shot, since = (cut[1], lt - cut[0]) if cut and lt >= cut[0] else (cam, lt)
            if shot is None:
                img = np.zeros((270, 480, 3))
            else:
                pos, look, fov = shot
                img = render((pos + norm(look - pos) * min(since * 0.04, 0.25), look, fov), cast, d, t + lt)
            fade = 1.0 if b.kind == CARD else (min(1.0, lt / hold) if b.kind == FADE_OUT else (max(0.0, 1.0 - lt / hold) if b.kind == FADE_IN else 0.0))
            if first and b.kind != CARD:
                fade = max(fade, 1.0 - lt / 0.4)
            shown = int(lt * 45.0) if b.kind == SAY else None
            frames.append(dress(img, b, shown=shown, bars=min(1.0, (t + lt) / 0.4), fade=min(1.0, max(0.0, fade))))
        t += hold
        first = False
        for a in cast:
            if t - a.emote_at > 2.2:
                a.emote, a.emote_at = E_NONE, -9.0
    frames = [f.quantize(colors=128, method=2, dither=0) for f in frames]   # (octree: the few white letters keep their white)
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / fps), loop=0, optimize=False)
    print("%s: %d frames (%.1f s) -> %s" % (sc["name"], len(frames), len(frames) / fps, out))


def main(argv):
    mode = argv[1] if len(argv) > 1 else "report"
    if mode == "report":
        return 1 if report_story() else 0
    if mode == "shots":
        return 1 if report_shots() else 0
    if mode in ("board", "film") and len(argv) >= 5:
        answer = int(argv[5]) if len(argv) > 5 else 1
        (storyboard if mode == "board" else film)(int(argv[2]), int(argv[3]), argv[4], answer)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
