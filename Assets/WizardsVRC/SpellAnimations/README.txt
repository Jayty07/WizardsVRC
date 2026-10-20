Spell cast animations (cinematics)

Drop a prefab here named after a spell's id or exact name, e.g. "104.prefab" or "Blazing Wyrm.prefab",
then run WizardsVRC > Rebuild MainWorld Scene. During combat the prefab is placed at the caster's seat, facing the
first target, and switched on (restarting its Animator / particle systems) for that spell's turn. Spells without a
prefab use the default glowing orb. Keep each cinematic under ~1.5 seconds for now (BattleManager.actionSeconds = 1.6),
or raise actionSeconds in the scene builder.

id   school    spell
100  Fire      Ember Bolt
101  Fire      Flame Lash
102  Fire      Cinder Storm
103  Fire      Inferno Pillar
104  Fire      Kindling Rite
105  Fire      Blazing Wyrm
106  Fire      Sunfire Cataclysm
110  Ice       Frost Shard
111  Ice       Rime Spear
112  Ice       Glacier Fall
113  Ice       Hoarfrost Nova
114  Ice       Frostward
115  Ice       Frozen Colossus
116  Ice       Winter's Wrath
120  Storm     Spark Jolt
121  Storm     Arc Lance
122  Storm     Thunderclap
123  Storm     Tempest Crown
124  Storm     Static Charge
125  Storm     Skyfall Leviathan
126  Storm     Maelstrom Eye
130  Life      Thornlash
131  Life      Verdant Bloom
132  Life      Rejuvenate
133  Life      Grove Guardian
134  Life      Seedling Blessing
135  Life      Wildwood Stampede
136  Life      Bloom of Renewal
140  Death     Shade Touch
141  Death     Grave Chill
142  Death     Wither
143  Death     Soul Siphon
144  Death     Veil of Dread
145  Death     Bone Dragon
146  Death     Requiem of Ash
150  Myth      Phantom Strike
151  Myth      Mirage Blade
152  Myth      Golem Fist
153  Myth      Legend's Echo
154  Myth      Mirror Image
155  Myth      Stone Basilisk
156  Myth      Fabled Hydra
160  Balance   Prism Spark
161  Balance   Arcane Scale
162  Balance   Harmonic Pulse
163  Balance   Equilibrium Ruin
164  Balance   Equanimity
165  Balance   Sandstorm Sphinx
166  Balance   Grand Convergence
200  General   Ward
201  General   Mend
202  General   Empower
203  General   Steady Mind
