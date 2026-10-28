Spell cast animations (cinematics)

Drop a prefab here named after a spell's id or exact name, e.g. "104.prefab" or "Blazing Wyrm.prefab",
then run WizardsVRC > Rebuild MainWorld Scene. During combat the prefab is placed at the caster's seat, facing the
first target, and switched on (restarting its Animator / particle systems) for that spell's turn. Spells without a
prefab use the default glowing orb. Keep each cinematic under ~1.5 seconds for now (BattleManager.actionSeconds = 1.6),
or raise actionSeconds in the scene builder.

id   school    lv  spell
100  Fire      1   Ember Imp
101  Fire      5   Smoldering Brand
102  Fire      10  Ember Edge
103  Fire      12  Cinder Snare
104  Fire      16  Cinder Rain
105  Fire      22  Ember Aegis
106  Fire      26  Pyre Hawk
107  Fire      28  Magma Hound
108  Fire      33  Stoke the Embers
109  Fire      35  Wildfire Oath
110  Fire      42  Molten Behemoth
111  Fire      48  Sunfire Wyrm
112  Ice       1   Frost Mite
113  Ice       5   Rime Challenge
114  Ice       10  Rime Edge
115  Ice       12  Frost Snare
116  Ice       16  Hoarfrost Gale
117  Ice       22  Frostward
118  Ice       26  Frost Wyvern
119  Ice       28  Rime Sentinel
120  Ice       33  Permafrost Bulwark
121  Ice       35  Winter's Bastion
122  Ice       42  Frozen Colossus
123  Ice       48  Winter's Wrath
124  Storm     1   Spark Sprite
125  Storm     5   Thunder Hare
126  Storm     10  Static Charge
127  Storm     12  Thunder Snare
128  Storm     16  Thunderclap
129  Storm     22  Storm Aegis
130  Storm     26  Skyfall Serpent
131  Storm     28  Thunder Golem
132  Storm     33  Gale Mark
133  Storm     35  Overcharge
134  Storm     42  Skyfall Leviathan
135  Storm     48  Maelstrom Eye
136  Life      1   Thornling
137  Life      5   Verdant Bloom
138  Life      10  Thorn Edge
139  Life      12  Bramble Snare
140  Life      16  Rejuvenate
141  Life      22  Verdant Aegis
142  Life      26  Grove Stag
143  Life      28  Treant Warden
144  Life      33  Wellspring
145  Life      35  Bloom of Renewal
146  Life      42  Wildwood Stampede
147  Life      48  Verdant Titan
148  Death     1   Shade Wisp
149  Death     5   Grave Ghast
150  Death     10  Grave Edge
151  Death     12  Hex Snare
152  Death     16  Soul Swarm
153  Death     22  Veil of Dread
154  Death     26  Night Leech
155  Death     28  Bone Thrall
156  Death     33  Dread Mark
157  Death     35  Dark Tithe
158  Death     42  Bone Dragon
159  Death     48  Requiem of Ash
160  Myth      1   Mire Imp
161  Myth      5   Mirage Lancer
162  Myth      10  Fable Edge
163  Myth      12  Mirage Snare
164  Myth      16  Clay Golemling
165  Myth      22  Unweave
166  Myth      26  Stone Basilisk
167  Myth      28  Stone Colossus
168  Myth      33  Sundering Gaze
169  Myth      35  Riddle of Iron
170  Myth      42  Fabled Hydra
171  Myth      48  Titan of Legends
172  Balance   1   Prism Spark
173  Balance   5   Sand Djinn
174  Balance   10  Accord Edge
175  Balance   12  Sand Snare
176  Balance   16  Sandstorm
177  Balance   22  Balance Aegis
178  Balance   26  Sphinx of Scales
179  Balance   28  Sand Golem
180  Balance   33  Elemental Tri-Blade
181  Balance   35  Spirit Tri-Blade
182  Balance   42  Sandstorm Sphinx
183  Balance   48  Grand Convergence
200  General   1   Tower Ward
201  General   2   Mend
202  General   3   Empower
203  General   4   Steady Mind
