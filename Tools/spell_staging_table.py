"""Prints the spell staging table (SPELL_ANIMATIONS.md) from the same formula the game uses: CardDatabase.Plan* and
SpellCinematic.PlanVolleys. Run it after changing either to see what every pip cost now gets.

    python3 Tools/spell_staging_table.py
"""
# Per delivery (index = CardDatabase.CINE_*): seconds from leaving the caster to hitting, and how long the release lasts.
FLIGHT = [1.4, 0.8, 1.3, 1.4, 2.2, 1.9, 1.5, 0.7, 2.0, 0.85, 1.7, 1.6, 1.75]
RELEASE = [2.0, 1.4, 1.9, 2.0, 2.8, 2.7, 2.1, 2.1, 2.6, 1.4, 2.3, 2.2, 2.35]
CAST_SOUND = 3.0          # the school's cast sound (2.9 to 3.8 s)
IN, OUT = 0.9, 0.8        # BattleManager.cineInSeconds, cineOutSeconds


def unleash(pips, lo=5.0, hi=20.0):
    return lo + (hi - lo) * (max(1, min(7, pips)) - 1) / 6.0


def steps(u): return (u - 5.0) / 2.5
def surge(u): return max(0.3, 0.5 + 0.15 * steps(u))
def form(u): return max(0.3, 0.6 + 0.3 * steps(u))
def afterglow(u): return max(0.7, 1.0 + 0.2 * steps(u))
def waves(u): return max(0, min(4, int(round(max(0.0, steps(u)) * 0.8))))
def gather(pips, per=0.5): return per * (max(1, min(7, pips)) - 1)


def volleys(u, delivery=1):
    """Each volley's (slot start, flies, hits, ends), seconds from the release: the waves, then the finisher."""
    bar, strike, w = surge(u) + form(u), u - afterglow(u), waves(u)
    unit = (strike - bar) / (w + 1.5)
    out = []
    for k in range(w):
        start, end = bar + k * unit, bar + (k + 1) * unit
        hit = end - min(0.45, unit * 0.2)
        flight = min(FLIGHT[3] * 0.9, (hit - start) * 0.75)
        out.append((start, hit - flight, hit, end))
    start = bar + w * unit
    flight = min(FLIGHT[delivery], (strike - start) * 0.75)
    out.append((start, strike - flight, strike, min(u - 0.05, strike + RELEASE[delivery] - FLIGHT[delivery])))
    return out


def tier(pips): return 4 if pips >= 7 else 3 if pips >= 5 else 2 if pips >= 3 else 1


def table(lo, hi, with_gather):
    print('| Pips | Tier | Charge | Unleash | Surge | Form | Waves | Each wave | Finisher | Hit lands at | Afterglow | Whole turn |')
    print('| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
    for p in range(1, 8):
        u = unleash(p, lo, hi)
        v = volleys(u)
        g = gather(p) if with_gather else 0.0
        wave = '%.2f' % (v[0][3] - v[0][0]) if len(v) > 1 else '-'
        print('| %d | %d | %.1f | %.1f | %.2f | %.2f | %d | %s | %.2f | %.2f | %.2f | %.1f |' % (
            p, tier(p), g + CAST_SOUND, u, surge(u), form(u), waves(u), wave, v[-1][2] - v[-1][0], u - afterglow(u), afterglow(u),
            IN + g + CAST_SOUND + u + OUT))


if __name__ == '__main__':
    print('Wizards and bosses\n')
    table(5.0, 20.0, True)
    print('\nOther monsters\n')
    table(3.5, 10.0, False)
    # Nothing overlaps or runs backwards, whatever the delivery.
    for lo, hi in ((5.0, 20.0), (3.5, 10.0)):
        for p in range(1, 8):
            u = unleash(p, lo, hi)
            for d in range(1, 13):
                last = surge(u) + form(u)
                for (start, fly, hit, end) in volleys(u, d):
                    assert start >= last - 1e-6 and start < fly < hit <= end + 1e-6 and hit - fly >= 0.3, (p, d)
                    last = end if hit < u - afterglow(u) - 1e-6 else last
