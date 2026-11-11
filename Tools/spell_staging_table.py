"""Prints the spell staging table (SPELL_ANIMATIONS.md) from the same formula the game uses: BattleManager.CineUnleash,
CardDatabase.Plan* and SpellCinematic.PlanBeats. Run it after changing any of them to see what every pip cost now gets.
The display is its own length (5 s, whatever the spell); the arrival, attack, result and exit share the rest.

    python3 Tools/spell_staging_table.py
"""
# Per delivery (index = CardDatabase.CINE_*): seconds from leaving the caster to hitting, and how long the release lasts.
FLIGHT = [1.4, 0.8, 1.3, 1.4, 2.2, 1.9, 1.5, 0.7, 2.0, 0.85, 1.7, 1.6, 1.75]
IN, OUT = 0.9, 0.8                      # BattleManager.cineInSeconds, cineOutSeconds
DISPLAY = 5.0                           # CardDatabase.cineDisplaySeconds
REST_MIN, REST_MAX = 3.6, 5.7           # BattleManager.spellRestSecondsMin, spellRestSecondsMax
GATHER = 0.1                            # gatherPerPip
EXPEND, EXPEND_MAX = 0.4, 2.0           # expendSeconds, expendMaxSeconds
CAST_SOUNDS = {'Fire': 2.9, 'Ice': 3.77, 'Storm': 3.46, 'Life': 3.17, 'Death': 3.0, 'Myth': 3.67, 'Balance': 3.06, 'Tech': 3.0}


def power(pips): return max(1, min(7, pips))
def rest(pips): return max(1.5, REST_MIN + (REST_MAX - REST_MIN) * (power(pips) - 1) / 6.0)
def gather(pips): return GATHER * (power(pips) - 1)
def unleash(pips, cast=0.0): return DISPLAY + rest(pips)
def whole(pips, cast): return IN + gather(pips) + cast + unleash(pips) + OUT
def waves(pips): return 1 if pips >= 5 else 0
def expend(n): return min(EXPEND_MAX, n * EXPEND)
def tier(pips): return 4 if pips >= 7 else 3 if pips >= 5 else 2 if pips >= 3 else 1


def beats(u):
    """Arrival, display, attack, result, exit: the display is its own length, the others share the rest."""
    d = min(max(0.5, DISPLAY), u * 0.75)
    r = u - d
    return r * 0.19, d, r * 0.48, r * 0.20, r * 0.13


def volleys(pips, attack, delivery=1):
    """Each volley's (slot start, flies, hits), seconds from the start of the attack: the wave (if any), then the finisher."""
    w = waves(pips)
    unit = attack / (w + 1.6)
    out = []
    for k in range(w):
        start, end = k * unit, (k + 1) * unit
        hit = end - min(0.3, unit * 0.2)
        out.append((start, hit - min(FLIGHT[3] * 0.8, (hit - start) * 0.8), hit))
    start = w * unit
    out.append((start, attack - min(FLIGHT[delivery], (attack - start) * 0.8), attack))
    return out


if __name__ == '__main__':
    cast = 3.0
    print('With a 3.0 s cast sound (they run from 2.9 to 3.8; a longer one makes the cast, and so the turn, that much longer)\n')
    print('| Pips | Tier | Cast | Arrival | Display | Attack | Strikes | Result | Exit | Whole turn | With 5 effects used |')
    print('| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
    for p in range(1, 8):
        u = unleash(p, cast)
        a, d, k, r, e = beats(u)
        print('| %d | %d | %.1f | %.2f | %.2f | %.2f | %d | %.2f | %.2f | %.1f | %.1f |' % (
            p, tier(p), IN + gather(p) + cast, a, d, k, waves(p) + 1, r, e + OUT, whole(p, cast), whole(p, cast) + expend(5)))
    longest = max(CAST_SOUNDS.values())
    print('\nThe shortest a spell can last: %.1f s (1 pip, the shortest cast sound, nothing used up)' % whole(1, min(CAST_SOUNDS.values())))
    print('The longest any spell can last: %.1f s (7 pips, the longest cast sound, five blades, traps and shields used)' % (whole(7, longest) + EXPEND_MAX))
    print('\nWhole turn by school, 1 pip and 7 pips:')
    for name, c in CAST_SOUNDS.items():
        print('  %-8s %.2f  %.2f' % (name, whole(1, c), whole(7, c)))
    for c in CAST_SOUNDS.values():
        for p in range(1, 8):
            u = unleash(p, c)
            assert abs(sum(beats(u)) - u) < 1e-6
            assert beats(u)[1] >= 5.0 - 1e-6, (p, c)       # the display: at least 5 s
            assert u >= 5.0                                 # and so the spell after its cast
            for d in range(1, 13):
                last = 0.0
                for (start, fly, hit) in volleys(p, beats(u)[2], d):
                    assert start >= last - 1e-6 and start < fly < hit and hit - fly >= 0.25, (p, d, start, fly, hit)
                    last = hit
