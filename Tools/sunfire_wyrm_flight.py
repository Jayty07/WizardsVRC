"""The Sunfire Wyrm's flight, worked out the way the game does it (Scripts/SunfireWyrm.cs is this file in UdonSharp, and
SpellCinematic's wyrm shots are the ones in 'shot' below). Kept apart from the renderer so the numbers can be checked
on their own:

    python3 Tools/sunfire_wyrm_flight.py            prints each case's path lengths, speeds and how level it flies
    python3 Tools/sunfire_wyrm_flight.py plots DIR  draws each case from above and from the side into DIR

The spell (SPELL_ANIMATIONS.md, "Sunfire Wyrm"): the dragon comes out of the caster's chest, twirls round them three
times climbing, sweeps down behind them and rears up over their shoulder (the display ends there, on its roar); in the
attack it flies at its target and through it (through every target in turn for an area cast, round the rim of the
ring), loops round, pulls up in front of them and breathes fire on them; then it burns away.

Everything is laid out on one curve. Waypoints are joined by a Catmull-Rom spline and walked into points a fixed
distance apart, each with an 'up' carried along the curve (so the body never flips), and the head's distance along it
is a function of time; each body segment sits a fixed distance behind the head on the same curve.
"""
import math
import sys
import numpy as np

UP = np.array([0.0, 1.0, 0.0])

SEGS = 24            # body segments behind the head
SPACING = 0.38       # metres between them (for a dragon of size 1)
NECK = 0.34          # from the head's pivot to the first
SUB = 8              # spline steps per waypoint
DENSE = 1500         # the most points the walked curve can have
STEP_MIN = 0.15      # metres between them (longer if the curve wouldn't fit)
ROLL = 70.0          # how fast it rights itself: degrees of roll per metre flown
BANK = 0.55          # how far it leans into a turn (metres of 'up' per unit of curvature)
TWIRLS = 3


def norm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v * 0.0


def flat(v):
    return np.array([v[0], 0.0, v[2]])


def ease(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


def trap(k, a, d):
    """How far along (0-1) after 'k' of the time: speeding up over the first 'a', steady, slowing over the last 'd'."""
    k = min(1.0, max(0.0, k))
    area = 1.0 - a * 0.5 - d * 0.5
    if k < a:
        return k * k / (2.0 * a) / area
    if k < 1.0 - d:
        return (k - a * 0.5) / area
    return 1.0 - (1.0 - k) * (1.0 - k) / (2.0 * d) / area


def signed_angle(a, b, axis):
    """Degrees from a to b about axis (Vector3.SignedAngle)."""
    a, b = norm(a), norm(b)
    ang = math.degrees(math.acos(min(1.0, max(-1.0, float(np.dot(a, b))))))
    return ang if np.dot(np.cross(a, b), axis) >= 0 else -ang


def angle_axis(deg, axis, v):
    """Quaternion.AngleAxis(deg, axis) * v."""
    axis = norm(axis)
    r = math.radians(deg)
    return v * math.cos(r) + np.cross(axis, v) * math.sin(r) + axis * np.dot(axis, v) * (1.0 - math.cos(r))


class Wyrm:
    # ------------------------------------------------------------------ the curve

    def plan(self, origin, fwd, flip, body, target_scale, targets):
        """'origin' the caster's feet in the middle of the ring, 'fwd' the way they face (flat), 'flip' +1 or -1 (which
        way it twirls), 'body' the caster's size, 'targets' the floor points of whoever it burns (one or more)."""
        O = np.array(origin, dtype=float)
        F = norm(flat(np.array(fwd, dtype=float)))
        R = np.cross(UP, F) * flip               # the side it turns towards
        self.O, self.F, self.R, self.flip, self.body = O, F, R, flip, body
        wrap = max(1.0, 0.5 + 0.5 * body)        # how wide it has to go to get round the caster
        size = min(wrap, 1.5)                    # how big the dragon itself is
        self.wrap, self.size = wrap, size
        self.ground = O[1]
        chest = 1.0 * target_scale
        way = []
        self.way = way

        def polar(a_deg, r, h):
            a = math.radians(a_deg)
            return O + (F * math.cos(a) + R * math.sin(a)) * r + UP * h

        # ---- out of the caster's chest, forward, and round into the first coil
        r0, r1 = 1.55 * wrap, 1.7 * wrap
        h0, h1 = 0.75 * size, 1.9 * body + 1.3 * size
        a0 = 60.0
        way.append(O + UP * (1.25 * body) + F * (0.2 * body))
        way.append(O + F * (0.85 * wrap) + R * (0.08 * wrap) + UP * (1.42 * body))
        way.append(O + F * (1.3 * wrap) + R * (0.5 * wrap) + UP * (0.65 * (1.42 * body) + 0.35 * h0))
        way.append(polar(38.0, 1.5 * wrap, 0.3 * (1.42 * body) + 0.7 * h0))
        # ---- three turns round them, climbing
        steps = TWIRLS * 12
        self.lap_way = []
        for k in range(steps + 1):
            f = k / steps
            if k > 0 and k % 12 == 0:
                self.lap_way.append(len(way))
            way.append(polar(a0 + 360.0 * TWIRLS * f, r0 + (r1 - r0) * f, h0 + (h1 - h0) * f))
        # ---- on round to the back, sweeping out and down to the floor behind them
        a1 = a0 + 360.0 * TWIRLS
        coil = 1.15 * size                       # the floor coil's radius
        back = r1 + 1.6 * size                   # how far behind the caster its middle is
        low = 0.34 * size
        C = O - F * back
        for k in range(1, 7):
            f = k / 6.0
            way.append(polar(a1 + 120.0 * f, r1 + (back + coil - r1) * f ** 1.3, h1 + (low - h1) * ease(f)))
        # ---- a quarter of a coil on the floor, coming round to face the caster's back
        for b in (225.0, 270.0):
            rad = math.radians(b)
            way.append(C + (F * math.cos(rad) + R * math.sin(rad)) * coil + UP * low)
        # ---- and it rears up over their shoulder: there it rests
        way.append(C - R * (coil * 0.95) + F * (0.75 * size) + UP * (low + 0.55 * size))
        way.append(C - R * (coil * 0.7) + F * (1.25 * size) + UP * (low + 1.6 * size))
        rest = O - F * (0.55 * wrap + 0.55 * size) - R * (0.3 * size) + UP * (1.9 * body + 1.35 * size)
        way.append(rest * 0.45 + way[-1] * 0.55 + UP * (0.25 * size) - F * (0.1 * size))
        self.mark_rest = len(way)
        way.append(rest)
        self.rest = rest

        # ---- the attack
        pts = [np.array(t, dtype=float) for t in targets]
        G = sum(pts) / len(pts)
        toward = norm(flat(G - O))
        if np.linalg.norm(toward) < 0.5:
            toward = F.copy()
        side = np.cross(UP, toward) * flip
        self.G, self.toward, self.side, self.chest = G, toward, side, chest
        hover_h = max(2.6, 1.9 * body + 1.0)
        top = hover_h + 1.4
        self.mark_target = []
        if len(pts) == 1:
            T = pts[0]
            dist = float(np.linalg.norm(flat(T - O)))
            hx = max(1.5 * size, dist - 4.4)
            H = O + toward * hx + UP * hover_h
            way.append(O + toward * (1.3 * wrap) + UP * (rest[1] - O[1] - 0.15))
            way.append(T - toward * 2.8 + UP * (chest + 0.7))
            self.mark_target.append(len(way))
            way.append(T + UP * chest)
            # over the top of them and back, then a turn down on to its mark
            way.append(T + toward * 1.2 + UP * (chest + 0.9))
            way.append(T + toward * 1.5 + side * 0.25 + UP * (chest + 2.5))
            way.append(T + toward * 0.4 + side * 0.6 + UP * (chest + 3.9))
            way.append(T - toward * 1.9 + side * 1.4 + UP * (chest + 4.0))
            way.append(H + (-toward * 0.2 + side * 2.2 + UP * 1.4) * size)
            way.append(H + (-toward * 0.98 + side * 1.88 + UP * 1.05) * size)
            way.append(H + (-toward * 1.3 + side * 1.1 + UP * 0.7) * size)
            way.append(H + (-toward * 0.98 + side * 0.32 + UP * 0.35) * size)
        else:
            # Round the rim through each of them in turn, the way it twirled, then spiralling in over the caster.
            ang = [signed_angle(toward, flat(p - O), UP) * flip for p in pts]
            order = sorted(range(len(pts)), key=lambda i: ang[i])
            pts = [pts[i] for i in order]
            ang = [ang[i] for i in order]
            rad = [float(np.linalg.norm(flat(p - O))) for p in pts]
            self.targets_sorted = pts

            def rim(a_deg, r, h):
                a = math.radians(a_deg)
                return O + (toward * math.cos(a) + side * math.sin(a)) * r + UP * h

            hx = 1.5 * size
            H = O + toward * hx + UP * hover_h
            way.append(rim(ang[0] * 0.75 - 15.0, 1.2 * wrap, rest[1] - O[1] - 0.1))
            way.append(rim(ang[0] - 30.0, rad[0] * 0.5, 0.55 * (rest[1] - O[1]) + 0.45 * chest))
            way.append(rim(ang[0] - 9.0, max(1.0, rad[0] - 1.0), chest + 0.6))
            for k in range(len(pts)):
                self.mark_target.append(len(way))
                way.append(pts[k] + UP * chest)
                if k + 1 < len(pts) and ang[k + 1] - ang[k] > 28.0:
                    way.append(rim(0.5 * (ang[k] + ang[k + 1]), 0.5 * (rad[k] + rad[k + 1]), chest + 0.2))
            aL, rL = ang[-1], rad[-1]
            way.append(rim(aL + 16.0, rL - 0.3, chest + 1.0))
            way.append(rim(aL + 42.0, rL * 0.74, chest + 1.0 + (top - chest - 1.0) * 0.6))
            way.append(rim(aL + 85.0, rL * 0.52, top))
            way.append(rim(aL + 130.0, rL * 0.36, top - 0.2))
            way.append(rim(aL + 175.0, max(rL * 0.27, 1.7 * wrap), hover_h + 0.7))
        if len(pts) == 1:
            self.targets_sorted = pts
        self.mark_hover = len(way)
        way.append(H)
        self.hover = H
        # ---- and away: up off its mark as it burns out
        way.append(H + toward * 1.0 + UP * 0.5)
        way.append(H + toward * 1.3 + UP * 1.9)
        way.append(H + toward * 0.6 + UP * 3.2)

        self._begin_walk()
        while not self.built:
            self.build_some(6)
        self.s_rest = self.way_s[self.mark_rest]
        self.s_hover = self.way_s[self.mark_hover]
        self.s_target = [self.way_s[i] for i in self.mark_target]
        self.s_lap = [self.way_s[i] for i in self.lap_way]
        self.s_end = self.total
        self.seg_back = [(NECK + i * SPACING) * size for i in range(SEGS)]
        self.length = self.seg_back[-1] + 0.7 * size

    def _begin_walk(self):
        way = self.way
        est = 0.0
        for i in range(len(way) - 1):
            est += float(np.linalg.norm(way[i + 1] - way[i]))
        self.step = max(STEP_MIN, est * 1.08 / (DENSE - 8))
        self.pt = [way[0].copy()]
        self.upv = [UP.copy()]
        self.way_s = [0.0] * len(way)
        self._i = 0
        self._prev = way[0].copy()
        self._carry = 0.0
        self._up = UP.copy()
        self._tan = norm(way[1] - way[0])
        self.built = False

    def build_some(self, segs):
        """Walks the next 'segs' waypoints' worth of the spline into points 'step' apart (a few each frame in the game)."""
        way = self.way
        n = len(way)
        while segs > 0 and self._i < n - 1:
            i = self._i
            p0, p1, p2, p3 = way[max(i - 1, 0)], way[i], way[i + 1], way[min(i + 2, n - 1)]
            a = (p3 - p0) * 0.5 + (p1 - p2) * 1.5
            b = p0 - p1 * 2.5 + p2 * 2.0 - p3 * 0.5
            c = (p2 - p0) * 0.5
            for k in range(1, SUB + 1):
                u = k / SUB
                q = ((a * u + b) * u + c) * u + p1
                d = float(np.linalg.norm(q - self._prev))
                while self._carry + d >= self.step and d > 1e-5 and len(self.pt) < DENSE:
                    f = (self.step - self._carry) / d
                    self._prev = self._prev + (q - self._prev) * f
                    self._add(self._prev)
                    d = float(np.linalg.norm(q - self._prev))
                    self._carry = 0.0
                self._carry += d
                self._prev = q
            self.way_s[i + 1] = (len(self.pt) - 1) * self.step + self._carry
            self._i += 1
            segs -= 1
        if self._i >= n - 1:
            self.built = True
            self.total = (len(self.pt) - 1) * self.step

    def _add(self, p):
        """One more point on the walked curve, and its 'up': the last one's carried along, rolling upright as it goes."""
        tan = norm(p - self.pt[-1])
        u = self._up - tan * float(np.dot(self._up, tan))
        u = norm(u) if np.linalg.norm(u) > 1e-4 else self._up
        level = 1.0 - abs(tan[1])
        if level > 0.12:
            want = UP - tan * tan[1] + (tan - self._tan) * (BANK / self.step)
            want = want - tan * float(np.dot(want, tan))
            if np.linalg.norm(want) > 1e-4:
                ang = signed_angle(u, want, tan)
                lim = ROLL * self.step * min(1.0, (level - 0.12) * 3.0)
                u = norm(angle_axis(min(lim, max(-lim, ang)), tan, u))
        self._up = u
        self._tan = tan
        self.pt.append(p.copy())
        self.upv.append(u.copy())

    def at(self, s):
        x = s / self.step
        k = int(math.floor(x))
        k = min(max(k, 0), len(self.pt) - 2)
        f = min(1.0, max(0.0, x - k))
        return self.pt[k] + (self.pt[k + 1] - self.pt[k]) * f

    def up_at(self, s):
        k = int(math.floor(s / self.step + 0.5))
        return self.upv[min(max(k, 0), len(self.upv) - 1)]

    # ------------------------------------------------------------------ the timing

    def schedule(self, t_unl, t_disp, t_exp, t_bar, t_strike, t_exit, t_after):
        self.t_unl, self.t_disp, self.t_exp, self.t_bar = t_unl, t_disp, t_exp, t_bar
        self.t_strike, self.t_exit, self.t_after = t_strike, t_exit, t_after
        show = t_exp - t_disp
        self.t_rest = t_disp + 0.80 * show
        self.t_roar = t_disp + 0.84 * show
        attack = t_strike - t_bar
        self.t_hover = t_strike - min(0.9, max(0.45, 0.24 * attack))
        # It draws its head back just before it goes (if it has had time to settle).
        self.draw = 0.45 * self.size if t_bar - self.t_rest > 0.5 else 0.0

    def head_s(self, t):
        """How far along the curve the head is at 't' (seconds from the start of the cast)."""
        if t <= self.t_unl:
            return 0.0
        if t < self.t_rest:
            return self.s_rest * trap((t - self.t_unl) / (self.t_rest - self.t_unl), 0.10, 0.30)
        if t < self.t_bar:
            return self.s_rest - self.draw * ease((t - (self.t_bar - 0.35)) / 0.35)
        if t < self.t_hover:
            s0 = self.s_rest - self.draw
            return s0 + (self.s_hover - s0) * trap((t - self.t_bar) / (self.t_hover - self.t_bar), 0.30, 0.35)
        if t < self.t_exit:
            return self.s_hover
        k = min(1.0, (t - self.t_exit) / max(0.05, self.t_after - self.t_exit))
        return self.s_hover + (self.s_end - self.s_hover) * (1.0 - (1.0 - k) * (1.0 - k))

    def passed(self, t):
        """How many of its targets the head has gone through."""
        if t < self.t_bar:
            return 0
        s = self.head_s(t)
        return sum(1 for x in self.s_target if s >= x)

    def laps(self, t):
        s = self.head_s(t)
        return sum(1 for x in self.s_lap if s >= x)

    # ------------------------------------------------------------------ where every part is

    def breath_aim(self, t):
        """Where the fire goes: swept along the targets and back (one target: round it a little)."""
        pts = self.targets_sorted
        span = max(0.2, self.t_exit - self.t_hover)
        u = 0.5 - 0.5 * math.cos(2.0 * math.pi * 1.5 * (t - self.t_hover) / span)
        if len(pts) == 1:
            w = (t - self.t_hover) * 5.0
            return pts[0] + UP * (self.chest * (0.8 + 0.25 * math.sin(w * 1.3))) + self.side * (0.35 * math.sin(w))
        x = u * (len(pts) - 1)
        k = min(int(math.floor(x)), len(pts) - 2)
        return pts[k] + (pts[k + 1] - pts[k]) * (x - k) + UP * (self.chest * 0.9)

    def pose(self, t):
        """The head and every segment at 't': positions, forwards, ups, scales (0 = not out yet or burnt away), and how
        far the jaw is open (0-1). Returns None before it is out."""
        if t < self.t_unl or t >= self.t_after:
            return None
        size = self.size
        s = self.head_s(t)
        P = self.at(s)
        ahead = self.at(s + 0.7 * size)
        D = norm(ahead - P)
        if np.linalg.norm(D) < 0.5:
            D = norm(P - self.at(s - 0.3 * size))
        U = self.up_at(s)
        jaw = 0.06
        # Out of the chest: jaws wide as it bursts out.
        jaw = max(jaw, 0.7 * (1.0 - ease((t - self.t_unl) / 0.7)))
        hold = 0.0
        aim = None
        if self.t_rest <= t < self.t_bar:
            # Come to rest: it looks out over the caster at whoever it is about to burn, and roars.
            hold = ease((t - self.t_rest) / 0.4) * (1.0 - ease((t - (self.t_bar - 0.2)) / 0.2))
            look = norm(self.G + UP * 1.2 - P)
            roar = ease((t - self.t_roar) / 0.18) * (1.0 - ease((t - (self.t_exp - 0.22)) / 0.22)) if t >= self.t_roar else 0.0
            aim = norm(look + UP * (0.55 * roar))
            jaw = max(jaw, roar)
            P = P + UP * (0.07 * size * math.sin(t * 2.6)) + self.R * (0.05 * size * math.sin(t * 1.7))
        elif self.t_bar <= t < self.t_hover:
            # Jaws open as it goes through them.
            for x in self.s_target:
                jaw = max(jaw, 0.75 * math.exp(-abs(s - x) / (1.6 * size)))
        elif t >= self.t_hover:
            hold = ease((t - self.t_hover) / 0.25)
            if t < self.t_exit:
                aim = norm(self.breath_aim(t) - P)
                jaw = max(jaw, 0.85 + 0.12 * math.sin(t * 31.0))
                P = P + UP * (0.06 * size * math.sin(t * 3.1)) - aim * (0.05 * size * math.sin(t * 23.0))
            else:
                k = (t - self.t_exit) / max(0.05, self.t_after - self.t_exit)
                aim = norm(norm(self.breath_aim(self.t_exit) - P) * (1.0 - k) + UP * (1.4 * k))
                jaw = max(jaw, 0.5 * (1.0 - k))
        if aim is not None and hold > 0.0:
            D = norm(D + (aim - D) * hold)
        level = UP - D * D[1]
        if hold > 0.0 and np.linalg.norm(level) > 0.2:
            U = angle_axis(signed_angle(U, level, D) * hold, D, U)
        U = norm(U - D * float(np.dot(U, D)))
        # The body: each segment a fixed distance behind the head on the curve, swaying, looking at the one ahead.
        burn = self.burn(t)
        segs = []
        ahead_p = P
        for i in range(SEGS):
            si = s - self.seg_back[i]
            if si <= 0.0:
                segs.append(None)
                continue
            p = self.at(si)
            u = self.up_at(si)
            d0 = norm(self.at(si + 0.2) - p)
            r = norm(np.cross(u, d0))
            sway = (0.03 + 0.11 * (i + 1) / SEGS) * size
            ph = t * 5.5 - i * 0.55
            p = p + r * (sway * math.sin(ph)) + u * (sway * 0.5 * math.cos(ph * 0.8))
            d = norm(ahead_p - p)
            if hold > 0.0 and i < 6:
                lv = UP - d * d[1]
                if np.linalg.norm(lv) > 0.2:
                    u = angle_axis(signed_angle(u, lv, d) * hold * (1.0 - i / 6.0), d, u)
            u = norm(u - d * float(np.dot(u, d)))
            grow = min(1.0, si / (0.7 * size))
            segs.append((p, d, u, grow * self.burnt(i, burn)))
            ahead_p = p
        head_scale = min(1.0, max(0.05, s / (0.7 * size))) * self.burnt(-1, burn)
        return {'head': (P, D, U, head_scale), 'segs': segs, 'jaw': jaw, 's': s, 'aim': aim}

    def burn(self, t):
        """How far the fire has eaten along it from the tail as it leaves (in segments; 0 until the exit)."""
        if t < self.t_exit:
            return 0.0
        return (t - self.t_exit) / max(0.05, self.t_after - self.t_exit) * (SEGS + 7)

    def burnt(self, i, burn):
        """What's left of segment i (-1 = the head) once the fire has got 'burn' segments along from the tail."""
        if burn <= 0.0:
            return 1.0
        from_tail = SEGS - 1 - i
        return min(1.0, max(0.0, 1.0 - (burn - from_tail) / 3.0))


# ---------------------------------------------------------------------- the cast's times, and the cases to look at

def cast_times(pips=7, cast_sound=2.9, expend=0.0, cine_in=0.9, gather_per_pip=0.1, display=5.0, rest_min=3.6, rest_max=5.7, cine_out=0.8):
    """SpellCinematic.Begin / PlanBeats for a staged spell (BattleManager.CineUnleash, CardDatabase.Plan*)."""
    power = max(1, min(7, pips))
    rest = max(1.5, rest_min + (rest_max - rest_min) * (power - 1) / 6.0)
    unleash = display + rest
    t = {}
    t['half'] = cine_in * 0.5
    t['cast'] = cine_in
    t['sound'] = cine_in + gather_per_pip * (power - 1)
    t['unl'] = t['sound'] + cast_sound
    t['after'] = t['unl'] + unleash + expend
    t['back'] = t['after'] + cine_out * 0.5
    t['end'] = t['after'] + cine_out
    d = min(max(0.5, display), unleash * 0.75)
    r = unleash - d
    t['disp'] = t['unl'] + r * 0.19
    t['exp'] = t['disp'] + d
    t['bar'] = t['exp'] + expend
    t['strike'] = t['bar'] + max(0.4, r * 0.48)
    t['exit'] = min(t['after'] - 0.05, t['strike'] + r * 0.20)
    return t


SEAT_RADIUS = 9.3 * 200.0 / 256.0     # WizardsVRCSceneBuilder.DuelSeatRadius


def seat(angle_deg):
    a = math.radians(angle_deg)
    return np.array([math.sin(a) * SEAT_RADIUS, 0.0, math.cos(a) * SEAT_RADIUS])


def case(name):
    """A caster in the middle of the ring facing +Z, and who they burn: 'one' (the seat straight ahead... the nearest
    to it), 'four' (the whole far side), 'two', or 'boss' (a boss three times a wizard's size casting it at one)."""
    monsters = [seat(a) for a in (-67.5, -22.5, 22.5, 67.5)]
    if name == 'one':
        targets, body = [monsters[2]], 1.0
    elif name == 'two':
        targets, body = monsters[1:3], 1.0
    elif name == 'boss':
        targets, body = [monsters[1]], 3.0
    else:
        targets, body = monsters, 1.0
    G = sum(targets) / len(targets)
    w = Wyrm()
    w.plan(np.zeros(3), G, -1.0, body, 1.0, targets)      # Sunfire Wyrm's recipe is mirrored: flip = -1
    t = cast_times()
    w.schedule(t['unl'], t['disp'], t['exp'], t['bar'], t['strike'], t['exit'], t['after'])
    w.times = t
    w.targets = targets
    return w


def report(name):
    w = case(name)
    t = w.times
    ts = np.linspace(w.t_unl, w.t_after, 1200)
    ss = np.array([w.head_s(x) for x in ts])
    v = np.diff(ss) / np.diff(ts)
    mid = ts[1:]
    show = mid < w.t_bar
    att = (mid >= w.t_bar) & (mid < w.t_exit)
    # How level it is: the angle between each point's up and the world's up squared off to the curve.
    tilt = []
    for k in range(1, len(w.pt)):
        tan = norm(w.pt[k] - w.pt[k - 1])
        lv = UP - tan * tan[1]
        if np.linalg.norm(lv) > 0.3:
            tilt.append((k * w.step, abs(signed_angle(w.upv[k], lv, tan))))
    spacing = [float(np.linalg.norm(w.way[i + 1] - w.way[i])) for i in range(len(w.way) - 1)]
    print('%-5s %d waypoints (%.2f-%.2f m apart), %d points %.3f m apart, %.1f m in all' % (
        name, len(w.way), min(spacing), max(spacing), len(w.pt), w.step, w.total))
    print('      rest at %.1f m, targets at %s, hover at %.1f m; dragon %.1f m long' % (
        w.s_rest, ', '.join('%.1f' % x for x in w.s_target), w.s_hover, w.length))
    print('      twirls done at %s s; rests %.2f, roars %.2f, goes %.2f, through them at %s, breathes from %.2f, strike %.2f, exit %.2f, gone %.2f' % (
        ', '.join('%.2f' % ts[np.searchsorted(ss, x)] for x in w.s_lap), w.t_rest, w.t_roar, w.t_bar,
        ', '.join('%.2f' % ts[min(len(ts) - 1, np.searchsorted(ss, x))] for x in w.s_target), w.t_hover, w.t_strike, w.t_exit, w.t_after))
    print('      top speed %.1f m/s in the display, %.1f m/s in the attack; never backwards but for drawing its head back: %s' % (
        v[show].max(), v[att].max(), bool((v[mid < w.t_bar - 0.37] > -1e-6).all() and (v[mid > w.t_bar + 0.01] > -1e-6).all())))
    for label, s in (('rest', w.s_rest), ('hover', w.s_hover)):
        near = [a for x, a in tilt if s - 3.0 <= x <= s]
        print('      roll off level over the last 3 m before the %s: %.0f deg at most' % (label, max(near) if near else 0.0))
    worst = max(tilt, key=lambda x: x[1])
    print('      most rolled anywhere: %.0f deg at %.1f m' % (worst[1], worst[0]))
    # The tightest it turns (a body %.2f m thick shouldn't fold).
    rad = []
    for k in range(2, len(w.pt)):
        t0, t1 = norm(w.pt[k - 1] - w.pt[k - 2]), norm(w.pt[k] - w.pt[k - 1])
        bend = float(np.linalg.norm(t1 - t0)) / w.step
        if bend > 1e-6:
            rad.append((1.0 / bend, k * w.step))
    tight = min(rad)
    print('      tightest turn: %.2f m radius at %.1f m' % tight)
    return w


def plots(out):
    from PIL import Image, ImageDraw
    cols = [(255, 120, 60), (255, 200, 80), (120, 220, 255), (190, 140, 255), (120, 255, 150), (255, 255, 255), (255, 90, 160)]
    for name in ('one', 'four', 'two', 'boss'):
        w = case(name)
        for part, times in (('display', [w.t_unl + 0.5, w.t_disp + 0.9, w.t_disp + 2.4, w.t_rest + 0.5]),
                            ('attack', [w.t_bar + 0.5, w.t_bar + 0.95, w.t_bar + 1.5, w.t_strike, w.t_exit + 0.35])):
            W, H, sc = 1500, 800, 30.0 if name != 'boss' else 20.0
            img = Image.new('RGB', (W, H), (18, 18, 26))
            d = ImageDraw.Draw(img)

            def top(p): return (380 + p[0] * sc, 520 - p[2] * sc)

            def side(p): return (900 + p[2] * sc, 740 - p[1] * sc * 1.2)

            for proj, label in ((top, 'from above (+z up the page)'), (side, 'from the side (+z right)')):
                o = proj(np.zeros(3))
                d.text((o[0] - 60, 14), label, fill=(200, 200, 200))
                d.line([proj(p) for p in w.pt[::2]], fill=(70, 70, 95), width=1)
                d.ellipse([o[0] - 6, o[1] - 6, o[0] + 6, o[1] + 6], outline=(255, 255, 255))
                for tg in w.targets:
                    q = proj(tg + (UP if proj is side else 0))
                    d.rectangle([q[0] - 5, q[1] - 5, q[0] + 5, q[1] + 5], outline=(255, 80, 80))
                for ti, tt in enumerate(times):
                    ps = w.pose(tt)
                    if ps is None:
                        continue
                    chain = [ps['head'][0]] + [sg[0] for sg in ps['segs'] if sg is not None and sg[3] > 0.05]
                    pp = [proj(p) for p in chain]
                    if len(pp) > 1:
                        d.line(pp, fill=cols[ti % len(cols)], width=4)
                    d.ellipse([pp[0][0] - 6, pp[0][1] - 6, pp[0][0] + 6, pp[0][1] + 6], fill=cols[ti % len(cols)])
                    # which way is 'up' along it
                    for sg in ps['segs'][::3]:
                        if sg is None:
                            continue
                        a, b = proj(sg[0]), proj(sg[0] + sg[2] * 0.45)
                        d.line([a, b], fill=(255, 255, 255), width=1)
            for ti, tt in enumerate(times):
                d.text((20, 40 + ti * 16), 't=%.2f  head %.1f m along' % (tt, w.head_s(tt)), fill=cols[ti % len(cols)])
            d.text((20, H - 24), '%s: %s' % (name, part), fill=(220, 220, 220))
            img.save('%s/wyrm_%s_%s.png' % (out, name, part))


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'plots':
        plots(sys.argv[2])
    else:
        for n in ('one', 'four', 'two', 'boss'):
            report(n)
