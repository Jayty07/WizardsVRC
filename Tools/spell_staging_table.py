"""Prints the spell staging table (SPELL_ANIMATIONS.md) from the same formula the game uses: BattleManager.CineUnleash,
CardDatabase.Plan* and SpellCinematic.PlanBeats. Run it after changing any of them to see what every pip cost now gets.

    python3 Tools/spell_staging_table.py
"""
# Per delivery (index = CardDatabase.CINE_*): seconds from leaving the caster to hitting, and how long the release lasts.
FLIGHT = [1.4, 0.8, 1.3, 1.4, 2.2, 1.9, 1.5, 0.7, 2.0, 0.85, 1.7, 1.6, 1.75]
IN, OUT = 0.9, 0.8                      # BattleManager.cineInSeconds, cineOutSeconds
WHOLE_MIN, WHOLE_MAX = 9.0, 12.0        # spellSecondsMin, spellSecondsMax
GATHER = 0.1                            # gatherPerPip
EXPEND, EXPEND_MAX = 0.4, 2.0           # expendSeconds, expendMaxSeconds
FLOOR = 3.0                             # unleashFloorSeconds
CAST_SOUNDS = {'Fire': 2.9, 'Ice': 3.77, 'Storm': 3.46, 'Life': 3.17, 'Death': 3.0, 'Myth': 3.67, 'Balance': 3.06, 'Tech': 3.0}


def power(pips): return max(1, min(7, pips))
def whole(pips): return WHOLE_MIN + (WHOLE_MAX - WHOLE_MIN) * (power(pips) - 1) / 6.0
def gather(pips): return GATHER * (power(pips) - 1)
def unleash(pips, cast): return max(FLOOR, whole(pips) - IN - OUT - gather(pips) - cast)
def waves(pips): return 1 if pips >= 5 else 0
def expend(n): return min(EXPEND_MAX, n * EXPEND)
def tier(pips): return 4 if pips >= 7 else 3 if pips >= 5 else 2 if pips >= 3 else 1


def beats(u):
    """Arrival, display, attack, result, exit: their shares of the unleash."""
    return u * 0.16, u * 0.16, u * 0.40, u * 0.17, u * 0.11


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
    print('With a 3.0 s cast sound (they run from 2.9 to 3.8; a longer one leaves less for the beats after it)\n')
    print('| Pips | Tier | Cast | Arrival | Display | Attack | Strikes | Result | Exit | Whole turn | With 5 effects used |')
    print('| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
    for p in range(1, 8):
        u = unleash(p, cast)
        a, d, k, r, e = beats(u)
        print('| %d | %d | %.1f | %.2f | %.2f | %.2f | %d | %.2f | %.2f | %.1f | %.1f |' % (
            p, tier(p), IN + gather(p) + cast, a, d, k, waves(p) + 1, r, e + OUT, whole(p), whole(p) + expend(5)))
    print('\nThe longest any spell can last: %.1f s' % (WHOLE_MAX + EXPEND_MAX))
    print('\nUnleash (the beats after the cast) by school, 1 pip and 7 pips:')
    for name, c in CAST_SOUNDS.items():
        print('  %-8s %.2f  %.2f' % (name, unleash(1, c), unleash(7, c)))
    for c in CAST_SOUNDS.values():
        for p in range(1, 8):
            u = unleash(p, c)
            assert abs(sum(beats(u)) - u) < 1e-6
            assert IN + gather(p) + c + u + OUT + EXPEND_MAX <= 14.0 + 1e-6, (p, c)
            for d in range(1, 13):
                last = 0.0
                for (start, fly, hit) in volleys(p, beats(u)[2], d):
                    assert start >= last - 1e-6 and start < fly < hit and hit - fly >= 0.25, (p, d, start, fly, hit)
                    last = hit
