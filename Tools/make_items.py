"""World 1's, Dustwind's (World 2) and Bigdig's (World 3) items: writes the item tables in Assets/WizardsVRC/Scripts/ItemDatabase.cs.

Every gear stat comes from one budget: what a full set (hat, robe, boots, wand, athame, amulet, ring) adds at a given
level (ANCH, interpolated between the anchor levels). Each slot takes its share of each stat (SHARE: robes carry
damage and power pip, boots resist and block, hats critical and accuracy, wands and athames pierce...). Quest rewards
are 12% above that and boss drops 25% above; school items lean into their school (SKEW: Fire hits harder but has less
health, Ice is tougher, Life has more mana and accuracy, Death pierces...). Item ids never change (they're in saves).

    python3 Tools/make_items.py          (run from the repo root; prints the table)
"""
import json, math, os, re
HAT, ROBE, BOOTS, WAND, ATHAME, AMULET, RING, DECK, POTION = range(9)
STATS = ['H','M','D','R','A','C','B','P','PP']
# Full-set totals (all seven gear slots) at anchor levels.
ANCH = {1:(60,7,3,3,2,2,2,2,0), 4:(110,10,6,4,4,3,2,1,4), 8:(200,16,11,7,6,5,4,2,9), 12:(320,24,17,10,8,8,6,3,15), 13:(350,26,18,11,9,9,7,3,16),
        18:(500,34,24,14,11,11,9,4,22), 22:(640,42,29,17,13,13,11,5,27), 25:(760,48,33,19,15,15,12,6,31),
        28:(860,53,37,21,17,17,13,7,35), 32:(990,60,42,24,19,19,15,8,40), 35:(1090,66,46,26,21,21,17,9,44)}
def budget(L):
    ks=sorted(ANCH)
    if L<=ks[0]: return ANCH[ks[0]]
    for a,b in zip(ks,ks[1:]):
        if a<=L<=b:
            t=(L-a)/(b-a); return tuple(x+(y-x)*t for x,y in zip(ANCH[a],ANCH[b]))
    return ANCH[ks[-1]]
# Each slot's share of each stat (columns sum to 1 across the seven slots).
SHARE = {
 HAT:   dict(H=.20,M=.20,D=0,  R=.20,A=.30,C=.50,B=0,  P=0, PP=0),
 ROBE:  dict(H=.30,M=.40,D=.35,R=.20,A=.20,C=0,  B=0,  P=0, PP=.50),
 BOOTS: dict(H=.25,M=0,  D=.15,R=.40,A=0,  C=0,  B=.30,P=0, PP=0),
 WAND:  dict(H=0,  M=0,  D=.30,R=0,  A=.30,C=.20,B=0,  P=.50,PP=0),
 ATHAME:dict(H=.05,M=0,  D=.20,R=0,  A=0,  C=0,  B=.30,P=.50,PP=0),
 AMULET:dict(H=.15,M=.20,D=0,  R=0,  A=0,  C=.30,B=0,  P=0, PP=.50),
 RING:  dict(H=.05,M=.20,D=0,  R=.20,A=.20,C=0,  B=.40,P=0, PP=0),
}
QUAL = {'sold':1.0,'quest':1.12,'boss':1.25,'start':1.0}
SKEW = {  # school items lean into their school's strengths
 0:dict(D=1.3,H=.85,C=1.25,R=.8), 1:dict(H=1.25,R=1.4,D=.8,B=1.25), 2:dict(D=1.45,H=.8,A=.8,R=.8),
 3:dict(H=1.15,M=1.3,A=1.25,D=.85), 4:dict(P=2.0,D=1.1,H=.95,C=1.1), 5:dict(PP=1.3,D=1.15,H=.9,B=.9),
 6:dict(R=1.2,A=1.15,D=.95,B=1.1),
}
SCH = ['Fire','Ice','Storm','Life','Death','Myth','Balance']
SCOL = [(0.95,0.35,0.15),(0.55,0.85,1.0),(0.65,0.4,0.95),(0.4,0.8,0.35),(0.45,0.4,0.55),(0.95,0.78,0.25),(0.85,0.6,0.4)]
items = []
def add(id,name,cat,lv,qual='sold',school=-1,desc='',color=None,unique=False,deck=(0,0),potion=(0,0),override=None):
    items.append(dict(id=id,name=name,cat=cat,lv=lv,qual=qual,school=school,desc=desc,color=color,unique=unique or qual in('quest','boss'),deck=deck,potion=potion,override=override))
B=None
# ---- tier 1: the starter kit and the first quest rewards (Lv 1-2)
add(1,"Training Staff",WAND,1,'start',desc="A plain practice staff every apprentice is handed on day one. It holds one basic spell from every school.",color=(0.55,0.4,0.25))
add(2,"Apprentice Robe",ROBE,1,'start',desc="Sturdy, if unflattering, academy robes.",color=(0.35,0.35,0.6))
add(3,"Apprentice Hat",HAT,1,'start',desc="A pointed felt hat. Mostly keeps the rain off.",color=(0.35,0.35,0.6))
add(4,"Apprentice Boots",BOOTS,1,'start',desc="Comfortable boots for long walks between classes.",color=(0.45,0.3,0.2))
add(30,"Bone Athame",ATHAME,1,'quest',desc="A ritual blade carved from a beast's tooth. Duel Master Holloway's prize for your first duel.",color=(0.85,0.8,0.7))
add(50,"Copper Ring",RING,1,'quest',desc="A simple band. Helps steady your aim.",color=(0.75,0.45,0.25))
add(40,"Moonstone Amulet",AMULET,2,'quest',desc="A pale stone that glows softly at night.",color=(0.8,0.85,0.95))
# ---- tier 2: castle shops and Sunpetal / Lantern Row (Lv 3-5)
add(23,"Scholar's Hat",HAT,3,desc="A tall hat stitched with tiny star charts.",color=(0.3,0.3,0.7))
add(25,"Silkweave Robe",ROBE,3,desc="Light robes woven with faint protective runes. Gloomfang's guards wore them.",color=(0.75,0.75,0.85))
add(24,"Traveler's Boots",BOOTS,3,desc="Well-worn boots that never seem to get muddy.",color=(0.5,0.35,0.2))
add(20,"Oakwood Wand",WAND,3,desc="A straight oak wand with a carved grip.",color=(0.6,0.45,0.25))
add(51,"Ring of Warding",RING,3,desc="Etched with a small warding sigil.",color=(0.5,0.6,0.75))
add(31,"Silver Athame",ATHAME,4,'quest',desc="A polished blade that turns aside hostile magic. Taken from the Briar Heart.",color=(0.8,0.8,0.85))
add(41,"Sunstone Amulet",AMULET,4,desc="Holds a sliver of trapped sunlight.",color=(0.95,0.75,0.2))
add(21,"Ember Wand",WAND,4,school=0,desc="Warm to the touch; sparks drift from the tip.",color=SCOL[0])
add(22,"Frost-Etched Wand",WAND,4,school=1,desc="Rimed with frost that never melts.",color=SCOL[1])
add(26,"Thunderhorn Wand",WAND,4,school=2,desc="Crackles with static whenever it is drawn.",color=SCOL[2])
add(80,"Thornbriar Wand",WAND,4,school=3,desc="A living briar wand that sprouts a new leaf every morning.",color=SCOL[3])
add(81,"Gravebone Wand",WAND,4,school=4,desc="Pale bone bound in black thread. It hums near graveyards.",color=SCOL[4])
add(82,"Fablewood Wand",WAND,4,school=5,desc="Carved with tiny scenes from old stories that seem to move.",color=SCOL[5])
add(83,"Sandglass Wand",WAND,4,school=6,desc="A slim hourglass of shifting sand set in a brass grip.",color=SCOL[6])
add(79,"Ashwick's Lantern",AMULET,5,'quest',desc="The Snuffer's own lantern, relit. It burns without oil.",color=(1.0,0.65,0.3))
add(10,"Minor Healing Potion",POTION,1,desc="A small red vial. Drink it from your backpack, or in battle (uses your turn).",color=(0.8,0.2,0.2),potion=(150,0))
add(11,"Mana Draught",POTION,1,desc="A swirling blue tonic. Spells cost 1 mana per pip; this tops you back up.",color=(0.2,0.4,0.85),potion=(0,12))
# ---- tier 3: school hats (Lv 6) and robes (Lv 7), Tidewatch / Kiln shops and rewards (Lv 6-9)
hats=[(84,"Flamewarden Hood","A hood of ember-red felt that never quite stops smoking."),(85,"Rimeguard Hood","Lined with frost-white fur; snow settles on it even indoors."),
      (86,"Stormcaller Hat","A wide-brimmed hat that makes your hair stand on end."),(87,"Briarleaf Hat","Woven from living leaves that turn toward the sun."),
      (28,"Gravewarden Cowl","A dark hood worn by the keepers of old crypts."),(88,"Fabler's Cap","A storyteller's cap with a long feather that writes on its own."),
      (89,"Equinox Hat","Half day-gold, half night-blue, stitched down the middle.")]
for s,(i,n,d) in enumerate(hats): add(i,n,HAT,6,school=s,desc=d,color=SCOL[s])
robes=[(90,"Emberweave Robe","Woven with threads of banked fire; warm in any weather."),(91,"Frostmantle Robe","Heavy blue robes that crackle with rime when you move."),
       (92,"Tempest Robe","Violet robes whose hem flutters in a wind no one else feels."),(27,"Verdant Vestments","Leafy green robes that smell faintly of spring."),
       (93,"Gloamshroud Robe","Grey-black robes that drink in lamplight."),(94,"Legendweave Robe","Embroidered with heroes and beasts from old tales."),
       (95,"Meridian Robe","Sand-coloured robes marked with the four winds.")]
for s,(i,n,d) in enumerate(robes): add(i,n,ROBE,7,school=s,desc=d,color=SCOL[s])
add(97,"Kilnstone Boots",BOOTS,7,desc="Hobnailed boots with soles of fired clay. They don't burn.",color=(0.55,0.35,0.25))
add(98,"Brass Signet",RING,7,desc="A heavy brass ring from a Kiln Street smith.",color=(0.8,0.65,0.3))
add(96,"Tidecaller Athame",ATHAME,7,desc="A curved blade of sea-glass that hums like a shell.",color=(0.45,0.7,0.75))
add(100,"Lamplight Pendant",AMULET,7,desc="A tiny lantern on a chain, lit by Lantern Row's candlemakers.",color=(1.0,0.8,0.45))
add(101,"Ashwood Wand",WAND,7,desc="Pale ash wood, oiled and balanced. A good wand for any school.",color=(0.75,0.65,0.5))
add(114,"Healing Potion",POTION,6,desc="A full red flask. Restores much more than a minor potion.",color=(0.85,0.15,0.2),potion=(300,0))
add(115,"Mana Tonic",POTION,6,desc="A deep blue tonic that fizzes on the tongue.",color=(0.15,0.35,0.9),potion=(0,24))
add(71,"Sea-Grey Boots",BOOTS,8,'quest',desc="Oiled sealskin boots that never slip on a wet pier.",color=(0.45,0.5,0.55))
add(70,"Harbormaster's Ring",RING,8,'quest',desc="Quill's own signet ring, worn smooth by salt and rope.",color=(0.7,0.65,0.55))
add(74,"Starglass Amulet",AMULET,8,'quest',desc="A sliver of the Observatory's old lens, set in silver.",color=(0.7,0.8,1.0))
add(108,"Grell's Tricorn",HAT,8,'boss',desc="Captain Marrow Grell's battered hat. There's a coded note still tucked in the band.",color=(0.25,0.22,0.28))
add(72,"Forgewarden's Cap",HAT,9,'quest',desc="A soot-stained leather cap with a brass brim. Smells of hot iron.",color=(0.5,0.32,0.18))
# ---- tier 4: Thornhollow and the Hollow Keep (Lv 10-13)
add(73,"Kilnfire Wand",WAND,10,'quest',desc="Quenched in a Kiln Street furnace; the grip is still warm.",color=(0.95,0.45,0.15))
add(75,"Grove Warden Robe",ROBE,10,'quest',desc="Moss-green robes of the Thornhollow wardens, patched with bark.",color=(0.3,0.5,0.25))
add(109,"Corps Lantern-Blade",ATHAME,10,'boss',desc="Captain Vesper's blade, its edge lit by a caged spark of stolen light.",color=(0.9,0.35,0.2))
add(113,"Kraken-Eye Ring",RING,10,'boss',desc="A cold gold ring set with one of the Kraken's smaller eyes. It blinks.",color=(0.35,0.75,0.7))
add(102,"Wardsteel Athame",ATHAME,11,desc="Castle-forged steel etched with the Starfall crest.",color=(0.7,0.72,0.8))
add(103,"Starfall Signet",RING,11,desc="A silver ring set with a chip of fallen star.",color=(0.65,0.75,1.0))
add(104,"Keepwatch Boots",BOOTS,11,desc="Tall boots worn by the castle's night watch.",color=(0.35,0.3,0.4))
add(105,"Crystalline Wand",WAND,11,desc="Grown, not carved, from Starfall crystal.",color=(0.6,0.8,1.0))
add(106,"Archivist's Hat",HAT,11,desc="Librarian Wren's design: a hat with pockets for notes.",color=(0.45,0.35,0.6))
add(107,"Starweave Robe",ROBE,11,desc="Robes woven with silver thread that glints like the night sky.",color=(0.25,0.3,0.6))
add(116,"Starlight Locket",AMULET,11,desc="A locket that holds a pinch of the rekindled crystal's light.",color=(0.75,0.85,1.0))
add(76,"Lightfoot Boots",BOOTS,11,'quest',desc="Light enough to catch a spy who runs.",color=(0.6,0.55,0.45))
add(110,"Legion Bulwark Ring",RING,11,'boss',desc="Sergeant Roan's ring, stamped with the Dominion's iron sun.",color=(0.5,0.55,0.65))
add(111,"Cipher Locket",AMULET,11,'boss',desc="Selwyn's locket. Inside is a cipher wheel instead of a portrait.",color=(0.45,0.4,0.6))
add(77,"Hollowbane Amulet",AMULET,12,'quest',desc="A shard of a shattered Hollow Lantern, its stolen light set free.",color=(0.6,0.5,0.9))
add(78,"Hollow Crown",HAT,13,'quest',desc="Morvane's crown. It is lighter than it looks, and colder.",color=(0.25,0.22,0.32))
add(112,"Hollow King's Mantle",ROBE,13,'boss',desc="Morvane's mantle, heavy with the light he stole and no longer holds.",color=(0.2,0.18,0.3))
# ---- decks (capacity, copies) unchanged in spirit; sold ones added between the drops
add(60,"Apprentice Deck",DECK,1,'start',desc="A plain leather deck every apprentice is given. Equip a deck to fight with the spells you put in it.",color=(0.5,0.35,0.22),deck=(20,4))
add(61,"Scholar's Deck",DECK,3,'boss',desc="A scholar's tooled-leather deck with room for a few more spells. Old Briarwick guards one.",color=(0.55,0.25,0.2),deck=(25,4))
add(117,"Traveler's Deck",DECK,5,desc="A canvas-bound deck with a strap for the road.",color=(0.5,0.45,0.3),deck=(27,4))
add(62,"Ironbound Deck",DECK,6,'boss',desc="Iron-cornered and heavy; it can hold five copies of a favourite spell.",color=(0.4,0.42,0.45),deck=(30,5))
add(118,"Seafarer's Deck",DECK,8,desc="Waxed against the salt; its pages never curl.",color=(0.3,0.45,0.55),deck=(33,5))
add(63,"Starfall Deck",DECK,10,'quest',desc="Embossed with the Starfall crest. A deck for a wizard who knows exactly what they want.",color=(0.3,0.3,0.6),deck=(40,6))
add(119,"Warden's Deck",DECK,12,desc="Bound in Thornhollow bark; the castle's best deck for coin.",color=(0.3,0.4,0.25),deck=(38,6))

# ================================================================== Dustwind (World 2, Lv 13-25); ids 120+
# Shop stock comes in three bands (Lv 13, 18 and 22) so Archrock's shops keep pace with the story.
add(120,"Drover's Hat",HAT,13,desc="A wide felt hat with a sweat-stained band. Keeps the desert sun off your nose.",color=(0.55,0.42,0.28))
add(121,"Prairie Robe",ROBE,13,desc="Sun-faded robes of tough canvas, cut for long days in the saddle.",color=(0.7,0.6,0.45))
add(122,"Boardwalk Boots",BOOTS,13,desc="Heeled boots that ring out on every plank in Archrock.",color=(0.45,0.3,0.18))
add(123,"Mesquite Wand",WAND,13,desc="Knotty desert hardwood, smoothed by a thousand hands.",color=(0.6,0.4,0.22))
add(124,"Frontier Athame",ATHAME,13,desc="A broad camp blade with an antler grip.",color=(0.75,0.72,0.65))
add(125,"Brass Compass Pendant",AMULET,13,desc="Always points back to Archrock, whichever way you're facing.",color=(0.85,0.65,0.3))
add(126,"Horseshoe Ring",RING,13,desc="A tiny bent horseshoe, worn points-up so the luck doesn't run out.",color=(0.6,0.6,0.62))
swands=[(127,"Cinderspur Wand","Its tip glows like a coal in a campfire."),(128,"Hailstone Wand","Cold as a desert night, and twice as clear."),
        (129,"Thunderhead Wand","Smells of rain before a storm breaks over the mesa."),(130,"Saguaro Wand","Carved from a fallen cactus rib; it still flowers once a year."),
        (131,"Rattlebone Wand","Rattles softly when trouble is near."),(132,"Tall-Tale Wand","Every story told about it is bigger than the last."),
        (133,"Dust Devil Wand","A little whirl of sand follows it everywhere.")]
for s_,(i,n,d) in enumerate(swands): add(i,n,WAND,14,school=s_,desc=d,color=SCOL[s_])
add(134,"Wide-Brim Hat",HAT,18,desc="Brim wide enough to shade a friend.",color=(0.42,0.32,0.22))
add(135,"Trailworn Robe",ROBE,18,desc="Patched and re-patched on the long trail out of town.",color=(0.55,0.45,0.35))
add(136,"Snakeskin Boots",BOOTS,18,desc="Shed by a rattlesnake, not taken from one. The bootmaker swears it.",color=(0.5,0.45,0.3))
add(137,"Ironwood Wand",WAND,18,desc="Desert ironwood: so dense it sinks in water.",color=(0.35,0.28,0.22))
add(138,"Railspike Athame",ATHAME,18,desc="Forged from a spike pried out of the Iron Line.",color=(0.5,0.5,0.55))
add(139,"Lantern-Ore Pendant",AMULET,18,desc="A chip of light-ore, freed from the Corps and glowing gently.",color=(1.0,0.75,0.35))
add(140,"Silver Dollar Ring",RING,18,desc="A silver coin hammered round a finger. It still shows the eagle.",color=(0.8,0.82,0.85))
srobes=[(141,"Ember Duster","A long coat that trails sparks instead of dust."),(142,"Frostline Duster","Frost creeps along its hem even at high noon."),
        (143,"Stormrider Duster","Its tails snap like a flag in a gale."),(144,"Bloomfield Duster","Wildflowers sprout from its pockets after rain."),
        (145,"Grim Drifter Duster","Faded black, and quiet as an empty street."),(146,"Storyteller's Duster","Every patch on it comes with a tale."),
        (147,"Sundial Duster","Its shadow always falls the right way for the hour.")]
for s_,(i,n,d) in enumerate(srobes): add(i,n,ROBE,19,school=s_,desc=d,color=SCOL[s_])
add(148,"Mesa Hat",HAT,22,desc="A tall crowned hat the color of red rock at sunset.",color=(0.65,0.3,0.2))
add(149,"Canyon Robe",ROBE,22,desc="Layered in the banded colors of the canyon walls.",color=(0.72,0.42,0.28))
add(150,"High Desert Boots",BOOTS,22,desc="Built for scree, sand and the long climb up Thunder Mesa.",color=(0.4,0.3,0.25))
add(151,"Thunderwood Wand",WAND,22,desc="Cut from a tree the lightning chose twice.",color=(0.45,0.4,0.55))
add(152,"Wardenstone Athame",ATHAME,22,desc="Chipped from a fallen Warden stone, with its blessing.",color=(0.7,0.6,0.5))
add(153,"Sunset Locket",AMULET,22,desc="Holds the last light of a desert evening.",color=(1.0,0.55,0.35))
add(154,"Prospector's Signet",RING,22,desc="Its seal is a pick crossed with a pan.",color=(0.85,0.7,0.35))
shats=[(155,"Blazebrim Hat","A brim that never stops smoldering."),(156,"Frostbrim Hat","Rime on the brim, even in July."),
       (157,"Stormbrim Hat","The band crackles when clouds gather."),(158,"Bloombrim Hat","A hatband of desert blossoms that never wilt."),
       (159,"Gravebrim Hat","Worn low, by those who walk at dusk."),(160,"Talebrim Hat","A feather in its band that once belonged to a legend, apparently."),
       (161,"Sunbrim Hat","Half gold, half shadow, like the mesa at noon.")]
for s_,(i,n,d) in enumerate(shats): add(i,n,HAT,23,school=s_,desc=d,color=SCOL[s_])
# ---- the level reward track's Frontier set (levels 14-24; see LevelRewards)
add(162,"Frontier Sixwand",WAND,14,'quest',desc="Six notches on the grip, one for every spell school but yours. Marshal Quickwater's gift.",color=(0.55,0.5,0.45))
add(163,"Trail Hat",HAT,15,'quest',desc="Dusty, dented and perfect.",color=(0.5,0.38,0.25))
add(165,"Frontier Duster",ROBE,18,'quest',desc="The long coat of a frontier wizard, cut to swirl when you draw your wand.",color=(0.5,0.4,0.3))
add(166,"Spurred Boots",BOOTS,20,'quest',desc="Their spurs jingle with every step. Sneaking is out of the question.",color=(0.35,0.25,0.18))
add(167,"Lodestone Amulet",AMULET,21,'quest',desc="A magnetic stone that pulls stray magic back to you.",color=(0.35,0.35,0.4))
add(168,"Silver Spur Ring",RING,23,'quest',desc="A ring shaped like a tiny spur. Its rowel still turns.",color=(0.82,0.84,0.88))
# ---- storyline rewards
add(180,"Tin Star Badge",AMULET,13,'quest',desc="Archrock's deputy badge. Wear it with pride, partner.",color=(0.75,0.75,0.78))
add(170,"Express Office Ring",RING,14,'quest',desc="Given to anyone who saves the mail. Stamped 'Archrock Express'.",color=(0.7,0.55,0.35))
add(171,"Creekwater Boots",BOOTS,16,'quest',desc="Still damp from the creek you set flowing again.",color=(0.35,0.5,0.6))
add(172,"Miner's Lamp Pendant",AMULET,18,'quest',desc="A tiny brass mine lamp that lights the way for those who are lost.",color=(1.0,0.85,0.45))
add(173,"Signalman's Athame",ATHAME,20,'quest',desc="Its edge is painted red and white like a signal arm.",color=(0.85,0.25,0.2))
add(176,"Pumpwright's Hat",HAT,20,'quest',desc="Oil-stained and proud of it. Archrock's water flows because of you.",color=(0.4,0.45,0.5))
add(177,"Bounty Hunter's Duster",ROBE,21,'quest',desc="Every outlaw in Dustwind knows the sound of this coat.",color=(0.3,0.25,0.22))
add(174,"Warden-Stone Ring",RING,22,'quest',desc="The Wardens' trust, set in stone.",color=(0.7,0.62,0.5))
add(175,"Ghostlight Lantern",AMULET,23,'quest',desc="A ghost's lantern, handed on with thanks. It never casts a shadow.",color=(0.6,0.9,0.85))
add(179,"Last Spike Wand",WAND,25,'quest',desc="Hammered from the Iron Line's last spike, which was never driven.",color=(0.9,0.75,0.4))
# ---- boss drops
add(181,"Bad Penny's Lucky Coin",RING,14,'boss',desc="A bent copper penny on a ring. Bad Penny says it was never lucky for her.",color=(0.75,0.45,0.25))
add(182,"Mesquite Heartwood Wand",WAND,16,'boss',desc="Heartwood from Old Mesquite himself, given back once he calmed down.",color=(0.4,0.6,0.3))
add(183,"Grist's Hard Hat",HAT,18,'boss',desc="Foreman Grist's dented hat, its lamp still burning stolen light.",color=(0.85,0.7,0.2))
add(184,"Vesper's Engine Coat",ROBE,20,'boss',desc="Captain Vesper's soot-black coat with brass buttons. It smells of coal smoke.",color=(0.22,0.2,0.24))
add(185,"Warden's Stoneplate Boots",BOOTS,22,'boss',desc="Heavy as the mesa and just as steady.",color=(0.6,0.55,0.5))
add(187,"Conductor's Pocketwatch",AMULET,23,'boss',desc="The Ghost Conductor's watch. It always reads midnight.",color=(0.65,0.75,0.8))
add(186,"Calloway's Iron Spike",ATHAME,25,'boss',desc="Ironhand Calloway's golden spike, meant for the Heartstone.",color=(0.85,0.7,0.3))
# ---- potions and decks
add(188,"Greater Healing Potion",POTION,13,desc="A big red bottle with a cork stopper. Tastes like cherries and gunpowder smoke.",color=(0.9,0.12,0.18),potion=(500,0))
add(189,"Strong Mana Tonic",POTION,13,desc="A dark blue tonic. Fizzes so hard the cork pops.",color=(0.12,0.3,0.85),potion=(0,36))
add(190,"Drifter's Deck",DECK,15,desc="A deck in a worn leather pouch, strung on a cord.",color=(0.5,0.38,0.25),deck=(42,6))
add(164,"Saddlebag Deck",DECK,16,'quest',desc="Room for more spells than you'll ever need. Then you'll need them.",color=(0.55,0.35,0.2),deck=(45,6))
add(191,"Wrangler's Deck",DECK,20,desc="Stitched with rawhide; it holds a herd of spells.",color=(0.45,0.3,0.2),deck=(48,6))
add(169,"Lawbringer's Deck",DECK,24,'quest',desc="Bound in a marshal's tooled leather, with a tin star on the cover.",color=(0.4,0.35,0.3),deck=(52,7))

# ---- Bigdig (World 3, Lv 25-35): the boroughs' shops, school gear, storyline rewards and boss drops (ids 200-263)
add(200,"Tunneler's Cap",HAT,26,desc="A padded leather cap with a lamp clip. Mind your head down there.",color=(0.45,0.35,0.25))
add(201,"Coalcloth Robe",ROBE,26,desc="Woven from fireproof coalcloth; soot slides right off.",color=(0.3,0.28,0.3))
add(202,"Hobnail Boots",BOOTS,26,desc="Iron-studded soles that grip wet stone and spark on cobbles.",color=(0.35,0.28,0.22))
add(203,"Sparkplug Wand",WAND,26,desc="A brass wand with a crackling gap at its tip.",color=(0.8,0.65,0.3))
add(204,"Rivet Athame",ATHAME,26,desc="Hammered from a single giant rivet. Surprisingly sharp.",color=(0.6,0.55,0.5))
add(205,"Gear Pendant",AMULET,26,desc="A little cog that turns all by itself.",color=(0.8,0.7,0.4))
add(206,"Nut-and-Bolt Ring",RING,26,desc="A nut, a bolt, and a kobold's idea of romance.",color=(0.65,0.62,0.6))
wands3=[(207,"Furnace Wand","Hot enough to light a forge, if you hold it the wrong way round."),(208,"Coolant Wand","Frost beads on it in the hottest smelter."),
        (209,"Arc-Coil Wand","A copper coil that hums with stored lightning."),(210,"Mossbulb Wand","Glowing cave-moss grows along its grip."),
        (211,"Gravedigger Wand","Carved from a deep-seam pit prop. It still smells of the dark."),(212,"Tall-Fuse Wand","Its fuse burns forever and never reaches the end."),
        (213,"Gyroscope Wand","Three brass rings spin round its tip, always level.")]
for s_,(i,n,d) in enumerate(wands3): add(i,n,WAND,27,school=s_,desc=d,color=SCOL[s_])
add(214,"Neon Brim Hat",HAT,30,desc="Its band glows pink in the dark, so your friends can find you on the Glimmer Mile.",color=(0.9,0.3,0.75))
add(215,"Velvet Gambler's Robe",ROBE,30,desc="Deep pockets. Very deep. Some say bottomless.",color=(0.45,0.15,0.3))
add(216,"Brass-Toe Boots",BOOTS,30,desc="Brass toecaps, for kicking doors and the odd dice.",color=(0.7,0.55,0.3))
add(217,"Lucky-Strike Wand",WAND,30,desc="Strikes like a match against any surface. Usually lights.",color=(0.85,0.3,0.2))
add(218,"Card-Edge Athame",ATHAME,30,desc="A blade as thin as a playing card, and just as tricky.",color=(0.85,0.85,0.9))
add(219,"Glowbulb Amulet",AMULET,30,desc="A little bulb of captured neon on a chain.",color=(0.4,0.95,0.85))
add(220,"Loaded Die Ring",RING,30,desc="A tiny die set in brass. It always lands on six. Always.",color=(0.95,0.95,0.9))
robes3=[(221,"Smelter's Robe","A heavy leather robe scorched black at the hem."),(222,"Coldforge Robe","Rime creeps along its seams even beside a furnace."),
        (223,"Dynamo Robe","Copper threads hum faintly when you move."),(224,"Mosslined Robe","Lined with soft glowing moss from the deep seams."),
        (225,"Undertunnel Robe","The colour of tunnels where no lamp has ever been."),(226,"Showman's Robe","Spangled, sequinned and impossible to ignore."),
        (227,"Assayer's Robe","A neat grey robe with a set of tiny scales embroidered on the breast.")]
for s_,(i,n,d) in enumerate(robes3): add(i,n,ROBE,31,school=s_,desc=d,color=SCOL[s_])
add(228,"Highroller Top Hat",HAT,33,desc="A tall black top hat with a gold band. Doors open for it.",color=(0.15,0.12,0.12))
add(229,"Gilded Waistcoat Robe",ROBE,33,desc="Gold thread, velvet lapels, and a pocket watch that's never wrong.",color=(0.75,0.6,0.2))
add(230,"Velvet Boots",BOOTS,33,desc="Soft as a whisper on a casino carpet.",color=(0.4,0.12,0.2))
add(231,"Golden Seam Wand",WAND,33,desc="Cut from a seam of singing gold. It hums when you cast.",color=(0.95,0.8,0.3))
add(232,"Vault Key Athame",ATHAME,33,desc="A key so big it doubles as a blade. It opens most things.",color=(0.8,0.7,0.45))
add(233,"Jackpot Locket",AMULET,33,desc="Open it and three cherries line up inside.",color=(0.9,0.2,0.25))
add(234,"Mother Lode Ring",RING,33,desc="A ring of raw gold that sings, very faintly, at night.",color=(1.0,0.85,0.35))
hats3=[(235,"Furnace Goggles Hat","Smoked-glass goggles on a heat-proof cap."),(236,"Frostlamp Hat","A miner's lamp that burns cold blue."),
       (237,"Tesla Crown","Little lightning arcs between its copper points."),(238,"Lantern-Moss Hat","Glowing moss grows thick along the brim."),
       (239,"Pit Boss Hat","Black felt, low brim, seen in the deepest seams."),(240,"Ringmaster's Hat","A tall striped hat for a spectacular entrance."),
       (241,"Scales Hat","A neat hat with a tiny balance on its crown, always level.")]
for s_,(i,n,d) in enumerate(hats3): add(i,n,HAT,34,school=s_,desc=d,color=SCOL[s_])
add(242,"Deepwarden's Lamp",AMULET,25,'quest',desc="Lit from the Mother Lode the year Bruma Stonehum became Deepwarden.",color=(1.0,0.82,0.4))
add(243,"Company Signet",RING,27,'quest',desc="The Bigdig Prospecting Company's seal: a nugget in a grinning mouth. A small share, Madame Ozza insists.",color=(0.9,0.7,0.2))
add(244,"Rustclaw Pick Athame",ATHAME,28,'quest',desc="Grandmaw Hesk's old pick, sharpened into a blade.",color=(0.65,0.35,0.2))
add(245,"Smuggler's Boots",BOOTS,30,'quest',desc="Big Pinch's own. They never leave a footprint.",color=(0.35,0.4,0.25))
add(246,"Glimmerfang Jacket",ROBE,31,'quest',desc="Neon-lined, loud, and impossible to lose in the dark. Tixi's own.",color=(0.6,0.25,0.8))
add(247,"Ward-Rune Pendant",AMULET,33,'quest',desc="Cut from one of Bigdig's old ward runes. It remembers the light.",color=(0.6,0.85,1.0))
add(248,"Handshake Ring",RING,33,'quest',desc="Every gang boss in Bigdig spat on it. Binding till the gold stops singing.",color=(0.85,0.75,0.5))
add(249,"Lucky Lou's Dice Ring",RING,32,'quest',desc="Lou's spare dice, set in brass. Roll them when you need a little luck.",color=(0.95,0.95,0.92))
add(250,"Runner's Boots",BOOTS,29,'quest',desc="Runner Pip's old boots, retired now that Pip has a cart.",color=(0.7,0.4,0.15))
add(251,"High Roller's Hat",HAT,34,'quest',desc="Lulu's gift. Wear it when you walk into the vault.",color=(0.2,0.15,0.15))
add(252,"All In Wand",WAND,35,'quest',desc="A wand of singing gold from the Mother Lode itself. Bigdig thanks you.",color=(1.0,0.85,0.3))
add(253,"Copperjaw's Knuckles",ATHAME,27,'boss',desc="Copperjaw Grubb's brass knuckles, filed into a blade.",color=(0.8,0.5,0.25))
add(254,"Vane's Drill-Bit Wand",WAND,28,'boss',desc="The tip of the Siphon Drill, still spinning slowly.",color=(0.5,0.5,0.55))
add(255,"Sorrel's Marine Coat",ROBE,30,'boss',desc="A Threadfleet officer's coat, blue and grey, salt-stained.",color=(0.25,0.3,0.45))
add(256,"Nightglass Monocle",HAT,31,'boss',desc="Agent Nightglass's monocle on a dark hood. It sees in the dark.",color=(0.15,0.15,0.22))
add(257,"Strake's Siphon Core",AMULET,33,'boss',desc="The humming heart of the siphon works, turned to better use.",color=(0.55,0.2,0.15))
add(258,"The Assayer's Scales",RING,34,'boss',desc="Quibb Silvernose's tiny scales. They weigh true now.",color=(0.85,0.8,0.65))
add(259,"Iron Wall Greaves",BOOTS,35,'boss',desc="Marshal Brannoch's greaves. They have never taken a step back.",color=(0.35,0.33,0.38))
add(260,"Superior Healing Potion",POTION,25,desc="A fizzing red bottle from Fuse & Fizz. Tastes like cinnamon and sparks.",color=(0.95,0.15,0.2),potion=(900,0))
add(261,"Fizzing Mana Bomb",POTION,25,desc="A blue sphere with a fuse. Drink it before it goes off.",color=(0.15,0.35,0.95),potion=(0,60))
add(262,"Tinker's Deck",DECK,27,desc="A deck in a riveted brass case. It clicks shut very satisfyingly.",color=(0.7,0.55,0.3),deck=(55,7))
add(263,"Highroller's Deck",DECK,33,'quest',desc="Gilt-edged cards in a velvet box. The house's own.",color=(0.6,0.15,0.2),deck=(60,7))

def stats(it):
    if it['cat'] in (DECK,POTION): return [0]*9
    b=budget(it['lv']); sh=SHARE[it['cat']]; q=QUAL[it['qual']]
    sk=SKEW.get(it['school'],{}) if it['school']>=0 else {}
    out=[]
    for k,(name) in enumerate(STATS):
        v=b[k]*sh[name]*q*sk.get(name,1.0)
        if name=='H': v=int(round(v/5.0))*5
        else: v=int(math.floor(v+0.5)) if v>=0.5 else 0
        out.append(v)
    return out
rows=[]
for it in sorted(items,key=lambda x:(x['lv'],x['cat'],x['id'])):
    it['stats']=stats(it); rows.append(it)
# report: full-set totals per tier using sold/start items
for it in rows:
    print(f"{it['id']:4} L{it['lv']:2} {['Hat','Robe','Boots','Wand','Athame','Amulet','Ring','Deck','Potion'][it['cat']]:6} {it['qual']:5} {('-' if it['school']<0 else SCH[it['school']]):7} {it['name']:24} " + ' '.join(f"{s}{v}" for s,v in zip(STATS,it['stats']) if v))
print(len(rows),'items; ids unique:',len({r['id'] for r in rows})==len(rows))

def arr(name, vals, typ='int'):
    return "        [HideInInspector] public %s[] %s = { %s };\n" % (typ, name, ', '.join(str(v) for v in vals))
out = "        // World 1's, Dustwind's and Bigdig's items: every stat comes from a level budget for a full set, split by slot (Tools/make_items.py).\n"
out += "        // Sorted by level, so the shops list them in order. Quest rewards and boss drops are unique (never sold).\n"
out += arr('ids', [r['id'] for r in rows]) + arr('categories', [r['cat'] for r in rows])
out += arr('levelReqs', [r['lv'] for r in rows]) + arr('schoolReqs', [r['school'] for r in rows])
for i, n in enumerate(['healthBonus', 'manaBonus', 'damageBonus', 'resistBonus', 'accuracyBonus', 'critBonus', 'blockBonus', 'pierceBonus', 'powerPipBonus']):
    out += arr(n, [r['stats'][i] for r in rows])
out += "        /// <summary>Quest rewards and boss drops: never sold in shops or rolled as random loot.</summary>\n"
out += arr('uniqueItems', [1 if r['unique'] else 0 for r in rows])
out += "        /// <summary>Decks only: how many spells the deck holds, and how many copies of one spell.</summary>\n"
out += arr('deckCapacity', [r['deck'][0] for r in rows]) + arr('deckMaxCopies', [r['deck'][1] for r in rows])
out += "        /// <summary>Potions only: health / mana restored when drunk (outside battle from the backpack, or in battle as your turn).</summary>\n"
out += arr('potionHealth', [r['potion'][0] for r in rows]) + arr('potionMana', [r['potion'][1] for r in rows])
out += "\n        [HideInInspector] public string[] names =\n        {\n" + ''.join('            "%s",\n' % r['name'] for r in rows) + "        };\n\n"
out += "        [HideInInspector] public string[] descriptions =\n        {\n" + ''.join('            "%s",\n' % r['desc'].replace('"', '\\"') for r in rows) + "        };\n\n"
out += "        [HideInInspector] public Color[] iconColors =\n        {\n" + ''.join('            new Color(%sf, %sf, %sf),\n' % tuple(r['color']) for r in rows) + "        };\n"
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
path = os.path.join(root, 'Assets', 'WizardsVRC', 'Scripts', 'ItemDatabase.cs')
src = open(path, encoding='utf-8').read()
a = src.index("        // World 1's")
b = src.index("        public int IndexOf(int itemId)")
open(path, 'w', encoding='utf-8').write(src[:a] + out + "\n" + src[b:])
print('wrote', path)
