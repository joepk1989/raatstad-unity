# Wegprofiel (verticaal alignement) van een wegennetwerk: algoritmen, haalbaarheid, snelle oplossing

Notatie in dit document: graaf G = (V, E), knoop i heeft terreinhoogte z_i (doel), gezochte weghoogte h_i; schakel (i,j) heeft horizontale lengte d_ij; maximale helling g (bv. 0,10). Harde eis: |h_i − h_j| ≤ g·d_ij. Vaste knopen T ⊂ V (bestaande wegen, brugdekken) met gegeven h_t.

---

## Key Question 1: Civil engineering — vertical alignment / earthwork optimization (klassieke methoden, doelfuncties, randvoorwaarden)

### Takeaway
De vakliteratuur (UBC Okanagan: Hare, Lucet en co-auteurs) lost het verticale alignement op als (gemengd-geheeltallig) lineair programma met een kwadratische spline als profiel, hellingsgrenzen aan de uiteinden van segmenten, en een doelfunctie van ontgravings-, ophoog- en transportkosten per m³; recente modellen (2025) behandelen ook netwerken met kruispunten, maar met rekentijden van seconden tot minuten op een MILP-solver. Oudere methoden zijn dynamisch programmeren (Goh et al. 1988) en genetische algoritmen (Fwa et al. 2002); geen van deze is bedoeld voor interactieve snelheid.

### Cited Findings
- Hare, Lucet & Rahman (EJOR 2015): MILP voor het verticale alignement met taluds ("side-slopes") en obstakels ("blocks"); doel is minimale kosten van grondverzet tussen wegsecties onder veiligheids- en bouwvoorschriften; de kosten zonder taluds kunnen meer dan 20% afwijken; het grondverzetschema zonder blokken is "unrealistic" — [IDEAS/RePEc abstract](https://ideas.repec.org/a/eee/ejores/v241y2015i3p631-641.html); [EconPapers](https://econpapers.repec.org/RePEc:eee:ejores:v:241:y:2015:i:3:p:631-641)
- Beiranvand, Hare, Lucet & Hossain (Eng. Optimization 2017, arXiv 1701.01768): multi-haul quasi-network-flow (MH-QNF) model; binnen 1% relatieve fout lost het 93% van de testproblemen op tegen 82% voor het complete-transportgraaf-model (CTG) en 0% voor QNF; ca. 8× sneller dan CTG — [arXiv 1701.01768](https://arxiv.org/abs/1701.01768v1)
- Unified Vertical Alignment (UVA) en convexe variant CUVA (arXiv 2508.15953, 2025): beslissingsvariabelen zijn de offset u_ij tussen weg en maaiveld per sectie en z_e per kruispunt, ontgraving/ophoging V⁺/V⁻ per sectie/materiaal/transportpad, stroomvariabelen voor transit-knopen, leen- en stortputten, en gehele getallen k voor het aantal "slabs" dat een talud benadert. Profiel: elke weg is een kwadratische spline (hoogte per segment een kwadratisch polynoom in de afstand). Doelfunctie: (p+y) per m³ ontgraving+laden, q per m³ ophoging, c per eenheid afstand transport, kosten op kruispuntstromen, leenput p+y+c·d̃, stortput q+c·d̃. Randvoorwaarden: minimale en maximale helling aan de segmentuiteinden (vgl. 8–9), continuïteit van hoogte en helling bij segmentovergangen (vgl. 3–4), gelijke hoogte op kruispunten (vgl. 5), weg-min-maaiveld = offset (vgl. 6), stroombehoud, slab-volumebegrenzingen. Het model noemt géén minimale boogstraal/K-waarde en géén vaste hoogtepunten zoals bruggen en geen zachte randvoorwaarden — [arXiv 2508.15953 (HTML)](https://arxiv.org/html/2508.15953v1)
- UVA/CUVA rekentijden: academische netwerken (3–4 wegen, 9–12 stations, 1 kruispunt) ca. 0,05 s; echt netwerk 1 (12 wegen, 327 stations, 5 kruispunten) UVA 18 s, CUVA 1,1 s; echt netwerk 2 (32 wegen, 1508 stations, 16 kruispunten) UVA geheugen op na 30 min, CUVA 61 s. Kruispunten: één virtuele transit-knoop per kruispunt; alleen begin of eind van een weg mag op een kruispunt liggen en elke weg ontmoet hooguit twee andere — [arXiv 2508.15953](https://arxiv.org/html/2508.15953v1)
- Voorgangers volgens hetzelfde artikel: Hare, Koch & Lucet (2011) vroege MILP met obstakels (rivieren, bergen); Koch & Lucet (2010) toonden dat gap- en hellingsrandvoorwaarden lineair blijven tot en met een kwadratische spline; Hossain (2013) meerdere materialen; Ayman et al. (2023) meerdere wegen in een netwerk (MR-QNF); Momo et al. convex QCQP-QNF — [arXiv 2508.15953](https://arxiv.org/html/2508.15953v1)
- Monnet, Hare & Lucet (Comput. Optim. Appl. 2020): snelle haalbaarheidstoets van de verticale-alignement-MILP; slechts een paar randvoorwaarden bepalen de haalbaarheid; gemiddeld 75× sneller om haalbaarheid te bewijzen en 10× sneller om een haalbare oplossing te bouwen — [Springer 10.1007/s10589-019-00160-3](https://link.springer.com/article/10.1007/s10589-019-00160-3) (abstract via zoekresultaat; volledige tekst achter login); Monnet, Hare & Lucet (2022): opeenvolgende lineaire relaxaties van een MILP met veel SOS2-variabelen voor stuksgewijs-lineaire functies convergeren naar een haalbare oplossing en verslaan CPLEX op grote instanties — [Springer 10.1007/s10589-021-00347-7](https://link.springer.com/article/10.1007/s10589-021-00347-7)
- Dynamisch programmeren: Goh et al. (1988) pasten als eersten DP toe op het tweedimensionale verticale alignement met grondverzetkosten uit as-hoogten; Göktepe, Altun & Ahmedzade (2009) combineerden discrete DP met de Weighted Ground Line Method (hypothetische grondlijn die ontgraving en ophoging balanceert); Göktepe, Lav & Altun (2005) gebruikten een kleinste-kwadratendoelfunctie in DP; sommige onderzoekers vonden DP inefficiënt voor 3D — [Tübitak 2009 (PDF)](https://aj.tubitak.gov.tr/engineering/issues/muh-09-33-2/muh-33-2-3-0904-1.pdf); [Göktepe et al., ITU](https://research.itu.edu.tr/en/publications/dynamic-optimization-algorithm-for-vertical-alignment-of-highways/)
- Fwa, Cha & Sim (J. Transp. Eng. 2002): genetisch algoritme dat kritieke hellinglengte, vaste-hoogtepunten ("fixed-elevation control") en niet-overlappen van horizontale en verticale bogen meeneemt; die eisen worden in conventionele alignement-optimalisatie "usually not addressed" — [ASCE 10.1061/(ASCE)0733-947X(2002)128:5(395)](https://ascelibrary.com/doi/10.1061/(ASCE)0733-947X(2002)128:5(395))
- Commerciële praktijk (Softree Optimal): randvoorwaarden zijn een verticale band, hellingsgrenzen, minimale boogstralen en controlepunten; aanbevolen werkwijze is beginnen met minimale randvoorwaarden en ze één voor één toevoegen met een Feasibility-check — [Softree Knowledge Base: Infeasible problems](https://support.softree.com/knowledge-base/optimization/softree-optimal-concepts/infeasible-problems)

### Inferences
- Alle civiele modellen zijn station-gebaseerd (een vast raster langs één as) en meten kosten in m³ met aparte prijzen voor ontgraven en ophogen; voor een spel is "afwijking van het terrein" (bv. Σ w_i (h_i − z_i)² of Σ |h_i − z_i|·breedte) een redelijke vereenvoudiging van die kostenfunctie. Een asymmetrische prijs (ophogen goedkoper dan ontgraven, of omgekeerd) is in zo'n doelfunctie eenvoudig als een gewicht per teken van (h − z).
- De MILP-aanpak is voor een interactieve bouwklik (<0,2 s, ~5000 knopen) niet geschikt; zelfs het convexe CUVA-model deed 1,1–61 s op 327–1508 stations. De harde-hellingsrandvoorwaarde zonder boogstralen en zonder transport (ons geval) is echter een veel kleiner, puur convex probleem (QP met verschilbeperkingen), zie vraag 2 en 5.

### Gaps
- De volledige tekst van Hare/Lucet/Rahman 2015 en Monnet 2020 (welke randvoorwaarden precies de haalbaarheid bepalen) staat achter een betaalmuur; alleen abstracts gelezen.
- Geen van de gevonden civiele papers behandelt knopen van graad ≥ 3 anders dan als "kruispunt waar wegen beginnen of eindigen" (UVA); een algemene graaf met tussenliggende splitsingen is in deze literatuur niet gevonden.

---

## Key Question 2: Het 1D-probleem met hellingsgrens — Lipschitz-regressie, isotone regressie, verschilbeperkingen, McShane–Whitney-omhullenden

### Takeaway
Haalbaarheid onder |h_i − h_j| ≤ g·d_ij met vaste knopen is een systeem van verschilbeperkingen: haalbaar dan en slechts dan als voor elk paar vaste knopen |h_s − h_t| ≤ g·dist(s,t) (kortste-padafstand met gewichten d_ij); de verzameling haalbare hoogten per knoop is het interval [L(x), U(x)] met L(x) = max_t (h_t − g·dist(x,t)) en U(x) = min_t (h_t + g·dist(x,t)), beide met twee multi-source Dijkstra-runs in O(E log V) te berekenen. Dit is exact de McShane–Whitney-constructie (Kyng et al. "vHigh/vLow"). De kleinste-kwadratenfit binnen die beperkingen is een QP; in 1D zijn er specialistische oplossers, maar voor ~30 vrije knopen is een eenvoudige iteratieve projectie ruim snel genoeg.

### Cited Findings
- Verschilbeperkingen x_j − x_i ≤ b_k komen overeen met een gewogen gerichte kant (v_i, v_j) met gewicht b_k; de kortste-padafstanden vanuit een extra bron v_0 (nulgewicht naar alle knopen) vormen een haalbare oplossing (CLRS Theorem 24.9); een negatieve cyclus betekent onhaalbaar; looptijd Bellman-Ford O((n+1)(n+m)) — [MIT OCW lec18 / CLRS 24.4](https://www.cs.rit.edu/~spr/COURSES/ALG/MIT/lec18.pdf); [walkccc CLRS 24.4 oplossingen](https://walkccc.me/CLRS/Chap24/24.4/)
- CLRS oef. 24.4-5: zonder extra bron kan men alle d-waarden op 0 initialiseren en Bellman-Ford draaien, O(nm). Oef. 24.4-8: de Bellman-Ford-oplossing is componentsgewijs maximaal (elke haalbare y heeft y_i ≤ δ(v_0,v_i)) en maximaliseert dus Σx_i; oef. 24.4-9: ze minimaliseert max{x_i} − min{x_i}; oef. 24.4-6: gelijkheden x_i = x_j + b worden twee ongelijkheden — [walkccc CLRS 24.4](https://walkccc.me/CLRS/Chap24/24.4/)
- McShane-uitbreiding: voor een k-Lipschitz f op U ⊂ (M,d) is f̃(x) := inf_{u∈U} { f(u) + k·d(x,u) } een k-Lipschitz-uitbreiding naar heel M (E. J. McShane, 1934) — [Wikipedia: Lipschitz continuity](https://en.wikipedia.org/wiki/Lipschitz_continuity); [Wikipedia: Kirszbraun theorem](https://en.wikipedia.org/wiki/Kirszbraun_theorem)
- Zoekresultaat-samenvatting (arXiv-preprints over AMLE): de McShane- en Whitney-uitbreidingen (sup resp. inf over de deelverzameling van f ± K·afstand) zijn de minimale en maximale 1-Lipschitz-uitbreidingen; elke andere minimale uitbreiding ligt er puntsgewijs tussen; een convexe combinatie λm + (1−λ)M is ook een minimale Lipschitz-uitbreiding, maar niet elke functie tussen m en M is dat; grafen worden gemetriseerd met de kortste-padafstand en discrete AMLE's zijn niet uniek — [arXiv 2601.09840](https://arxiv.org/pdf/2601.09840); [arXiv 1405.4809](https://arxiv.org/pdf/1405.4809) (uit zoekfragmenten; niet volledig gelezen)
- Kyng, Rao, Sachdeva & Spielman (COLT 2015), "Algorithms for Lipschitz Learning on Graphs": een "inf-minimizer" (minimale Lipschitz-uitbreiding) minimaliseert max_{(x,y)∈E} |v(x)−v(y)|/ℓ(x,y) gegeven terminalwaarden v_0 op T; **Lemma 3.5**: een uitbreiding met ‖grad‖_∞ ≤ α bestaat dan en slechts dan als elk terminalpad P voldoet aan ∇P(v_0) = (v_0(∂_0P) − v_0(∂_1P))/ℓ(P) ≤ α; de optimale α* is de helling van het steilste terminalpad. **Sectie 4**: met dist de kortste-padmetriek zijn vLow[α](x) = min_{t∈T}{v_0(t) + α·dist(x,t)} en vHigh[α](x) = max_{t∈T}{v_0(t) − α·dist(t,x)} beide inf-minimizers bij α = α*; hun gemiddelde is de inf-minimizer die in ℓ_∞ het dichtst bij alle inf-minimizers ligt. **Lemma 4.3**: pressure(x) > α ⟺ vHigh[α](x) > vLow[α](x). **Theorem 4.6**: CompInfMin loopt in gerandomiseerde O(m + n log n) tijd. Inf-minimizers zijn niet uniek — [arXiv 1505.00290 (ar5iv HTML)](https://ar5iv.labs.arxiv.org/html/1505.00290); [COLT 2015](https://proceedings.mlr.press/v40/Kyng15.html)
- Lipschitz-regressie in 1D: R-pakket `simest::cvx.lip.reg` minimaliseert de gewogen kwadratensom onder hellingsgrenzen tussen opeenvolgende gesorteerde punten (Lipschitz-constante L) en lost dat op via `nnls` — [CRAN simest cvx.lip.reg](https://rdrr.io/cran/simest/man/cvx.lip.reg.html)
- Mazumder et al. (JASA 2018, "Lipschitz convex regression"): kleinste-kwadratenschatter is een QP met O(n²) lineaire beperkingen, lastig voor grote n; zij gebruiken een augmented-Lagrangian-methode om te schalen — [MIT DSpace](https://dspace.mit.edu/handle/1721.1/120818)
- Twente-scriptie (2023): vergelijkt QP, Beliakov's algoritme (lagere complexiteit dan QP) en een lokaal optimalisatie-algoritme voor kleinste kwadraten over Lipschitz-functies; het lokale algoritme geeft vergelijkbare resultaten "in a fraction of the time" voor grote steekproeven — [essay.utwente.nl 97150](https://essay.utwente.nl/essays/97150)
- Isotone regressie op grafen: Kyng, Rao & Sachdeva (NeurIPS 2015) geven snelle algoritmen met bewijsbare garanties voor alle gewogen ℓ_p-normen via inwendige-puntmethoden met benaderende lineaire oplossingen; strikte isotone regressie wordt een lex-minimalisatie oplosbaar met n aanroepen van een inf-minimalisatieroutine (uit een Koreaanse samenvatting; niet geverifieerd) — [arXiv 1507.00710](https://arxiv.org/abs/1507.00710v2); [NeurIPS 2015](https://papers.nips.cc/paper/2015/hash/be53ee61104935234b174e62a07e53cf-Abstract.html); Pardalos & Xue: O(n log n) voor gerichte bomen, generalisatie van PAV — [Algorithmica (unpaywall)](https://unpaywall.org/10.1007%2FPL00009258)
- Patent US 6,996,507 (ontwerp van transportroutes): elk vast punt moet een controlepunt zijn; bij twee of meer vaste punten moeten de hellingen tussen de vaste punten vóór de optimalisatie gecontroleerd worden via het kortste pad ertussen — [USPTO 6996507](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/6996507) (uit zoekfragment; gescande PDF niet leesbaar)

### Inferences
- Vertaling naar ons probleem (eigen afleiding op basis van CLRS 24.4 en Kyng Lemma 3.5/Sectie 4): |h_i − h_j| ≤ g·d_ij is het paar h_i − h_j ≤ g·d_ij en h_j − h_i ≤ g·d_ij; met vaste knopen t is h_x ≤ h_t + g·dist(x,t) en h_x ≥ h_t − g·dist(x,t) langs elk pad, dus
  - U(x) = min_t [h_t + g·dist(x,t)]  (bovenste omhullende, Whitney/"vLow" bij Kyng)
  - L(x) = max_t [h_t − g·dist(x,t)]  (onderste omhullende, McShane/"vHigh" bij Kyng)
  - haalbaar ⟺ L(x) ≤ U(x) voor alle x ⟺ voor alle vaste paren |h_s − h_t| ≤ g·dist(s,t) (Lemma 3.5 met α = g).
  - Elke h met L ≤ h ≤ U en vaste waarden op T is níét automatisch Lipschitz (zie het "niet elke functie tussen m en M"-citaat); L en U zelf zijn het wel. Het clampen van het terrein z op [L, U] is dus alleen een startpunt, geen oplossing.
  - Omdat alle d_ij > 0 en g > 0 zijn de kantgewichten positief, dus Dijkstra (niet Bellman-Ford) volstaat: twee multi-source Dijkstra-runs (één met h_t als startlabel voor U, één met −h_t voor L) kosten O((V+E) log V). Op 5000 knopen en ~7500 kanten is dat in C# ruim onder een milliseconde (schatting, niet gemeten).
- Pseudocode haalbaarheidstoets + omhullenden:
  ```
  // U: multi-source Dijkstra, label(t) = h_t voor t in T, kantkosten g*d_ij
  U = Dijkstra(sources: {t: h_t}, cost: g*d)
  // L: idem met label(t) = -h_t, daarna negeren
  L = -Dijkstra(sources: {t: -h_t}, cost: g*d)
  feasible = forall x: L[x] <= U[x] - eps
  // steilste onhaalbare paar: vaste knoop t die L[x] levert en s die U[x] levert bij de grootste L[x]-U[x]
  ```
- De CLRS-oplossing (alle variabelen = kortste pad vanuit de bron) is de componentsgewijs maximale oplossing (24.4-8) — dat is precies U; de symmetrische constructie geeft L. Dit onderbouwt dat U en L de strakst mogelijke grenzen zijn.
- Het 1D-geval (één nieuwe weg, 30 knopen, beide einden vast) is een QP met 2·29 halfvlakbeperkingen plus doosgrenzen [L,U]; dat is klein genoeg om met iteratieve projecties (vraag 3) of een generieke actieve-set-QP in microseconden op te lossen; de specialistische O(n log n)-algoritmen (Beliakov, PAV-generalisaties) zijn hier onnodig.

### Gaps
- Geen primaire bron gevonden die expliciet "Lipschitz-uitbreiding op grafen via Dijkstra = McShane–Whitney" in deze bewoording zegt; de combinatie van Kyng et al. (Sectie 4, kortste-padmetriek, vLow/vHigh) met de McShane-formule op Wikipedia dekt het inhoudelijk wel.
- Het Beliakov-algoritme zelf (exacte complexiteit) is alleen via de Twente-scriptie bekend; de scriptie zelf is niet gelezen.

---

## Key Question 3: Op een graaf — Lipschitz-uitbreiding, ∞-Laplaciaan/AMLE, p-Laplaciaan, en iteratieve relaxatie (POCS/Dykstra); convergentie en wat er geminimaliseerd wordt

### Takeaway
De "absoluut minimale" Lipschitz-uitbreiding (AMLE, lex-minimizer) is uniek, stabiel en gelijk aan de limiet p→∞ van p-Laplaciaan-regularisatie; ze is exact te berekenen in Õ(mn) of iteratief via het midrange-filter h(x) ← ½(max_nabuur + min_nabuur) (gewogen), dat bewijsbaar convergeert maar zonder bekende snelheid. Een eenvoudige "halveer het teveel per schakel"-iteratie is de methode van afwisselende projecties (POCS): ze convergeert naar één of ander haalbaar punt maar niet naar de dichtstbijzijnde; met Dykstra's correctietermen convergeert ze (lineair voor polyeders) naar de echte projectie van het terrein op de haalbare verzameling, d.w.z. de kleinste-kwadratenoplossing. Geen van deze AMLE-methoden houdt rekening met het terrein; daarvoor is een kleinste-kwadraten- of gepenaliseerde formulering nodig.

### Cited Findings
- Kyng et al.: de lex-minimizer (AMLE) is voor een goed gestelde instantie uniek (Theorem 3.3) en wordt verkregen door herhaaldelijk het "steilste fixeerbare pad" te fixeren (Meta-Lex); **Theorem 3.10 (max-min-middeling)**: de lex-minimizer is de unieke uitbreiding waarbij in elke niet-terminale knoop de grootste uitgaande gradiënt gelijk is aan min de kleinste; voor eenheidslengten lex(x) = ½(max_{y∼x} lex(y) + min_{z∼x} lex(z)); **Theorem 3.8**: minimizers v_p van ‖grad v‖_p convergeren voor p→∞ naar lex; **Corollary 3.7 (stabiliteit)**: verschillen de terminalwaarden hooguit ε, dan verschillen de lex-minimizers overal hooguit ε; **Theorem 4.4/Cor. 4.5**: SteepestPath in verwachte O(m + n log n), CompLexMin in O(n(m + n log n)); praktische variant CompFastLexMin zonder betere bewezen grens maar "een paar minuten" op grafen met ~0,5·10⁶ knopen — [arXiv 1505.00290 (ar5iv)](https://ar5iv.labs.arxiv.org/html/1505.00290)
- Bačák, Hertrich, Neumayer & Steidl (2019/2020): gewogen ∞-Laplaciaan Δ_{w,∞} f(u) = ½( max_{v∼u} w(u,v)(f(v) − f(u)) + min_{v∼u} w(u,v)(f(v) − f(u)) ); de iteratie van Elmoataz et al. is f^{(r+1)} = f^{(r)} + τ·Δ_{w,∞} f^{(r)} met τ ∈ (0,1); in het ongewogen geval Δ_∞ f(u) = midrange(buren) − f(u). **Corollary 5.6**: voor scalaire functies en elke τ ∈ (0,1) convergeren de iteraties naar de minimale Lipschitz-uitbreiding (via Krasnoselskii–Mann, omdat het midrange-filter niet-expansief is in de ∞-norm); **Prop. 7.2**: de cyclische (Gauss–Seidel-)variant verkleint bij elke niet-triviale stap strikt de L-lex-ordening; er wordt geen convergentiesnelheid gegeven voor de midrange-iteratie; lex- en L-lex-minimale uitbreiding zijn hetzelfde en uniek (Theorem 3.2); ∞-harmonische uitbreidingen van vectorwaardige functies zijn niet uniek, scalaire wel (Lemma 5.4) — [arXiv 1903.04873 (ar5iv)](https://ar5iv.labs.arxiv.org/html/1903.04873)
- Dykstra's projectie-algoritme: met x_0 = r, p_0 = q_0 = 0: y_k = P_D(x_k + p_k); p_{k+1} = x_k + p_k − y_k; x_{k+1} = P_C(y_k + q_k); q_{k+1} = y_k + q_k − x_{k+1}; de rij x_k convergeert naar de projectie van r op C ∩ D; gewone afwisselende projecties (POCS, x_{k+1} = P_C(P_D(x_k))) geven bij een doorsnede met meer dan één punt "some arbitrary point in this intersection" — [Wikipedia: Dykstra's projection algorithm](https://en.wikipedia.org/wiki/Dykstra%27s_projection_algorithm)
- Dykstra convergeert lineair voor polyedrische verzamelingen (asymptotisch lineair voor halfvlakken); Bauschke, Burachik, Herman & Kaya tonen dat Dykstra willekeurig lang kan "stallen"; Han toonde dat Dykstra gelijk is aan afwisselende minimalisatie van het duale probleem — [Bauschke et al., Univ. Adelaide](https://digital.library.adelaide.edu.au/items/2d614926-3c1d-4fa2-a5be-5ddffe4b40a9); [Dykstra met polyedrische kegels (CONICET)](https://repositoriosdigitales.mincyt.gob.ar/vufind/Record/CONICETDig_b29b5aa760de37d235f6dc9e8ab47a59); [ar5iv 1601.01174](https://ar5iv.arxiv.org/html/1601.01174)
- p-Laplaciaan-regularisatie en ℓ_1/ℓ_0: Kyng et al. lossen ℓ_1-regularisatie op kanten/knopen als LP in Õ(m^{1.5}) op; ℓ_2-regularisatie met ℓ_0 op knoopwaarden is NP-hard (Theorem 7.4) — [arXiv 1505.00290](https://ar5iv.labs.arxiv.org/html/1505.00290)

### Inferences
- De bestaande code ("halveer het teveel op elke schakel") is per schakel precies de orthogonale projectie op het halfvlak h_i − h_j ≤ g·d_ij (beide knopen bewegen ½ van het teveel, tegengesteld), toegepast in een cyclus: dat is POCS. Gevolgen volgens de aangehaalde bronnen: (a) convergeert naar een haalbaar punt zodra er een is, (b) niet noodzakelijk naar het punt met minimale Σ(h−z)², (c) bij onhaalbaarheid convergeert het niet naar een haalbaar punt maar blijft het oscilleren/een cyclus tussen de verzamelingen volgen. Het minimaliseert dus "niets" in strikte zin.
- Dykstra-variant van dezelfde lus (eigen formulering van het algoritme hierboven, per halfvlak): houd per beperking c een correctievector (hier: één scalar, want de projectie verschuift h_i en h_j met ±a_c):
  ```
  h = clamp(z, L, U); for alle c: a_c = 0
  repeat sweeps:
    for elke schakel (i,j) en elk teken s in {+1,-1}:           // beperking s*(h_i - h_j) <= g*d_ij
      // herstel de vorige correctie
      h_i += s*a_c; h_j -= s*a_c
      excess = s*(h_i - h_j) - g*d_ij
      a_c = max(0, excess/2)                                     // projectie op halfvlak
      h_i -= s*a_c; h_j += s*a_c
    vaste knopen: h_t = h_t_fixed (projectie op een affiene verzameling: geen correctie nodig)
  until max excess < tol
  ```
  Dit convergeert naar de Euclidische projectie van z op {Lipschitz, vast}, dus naar argmin Σ(h_i − z_i)² (gewichten w_i via geschaalde coördinaten). Lineaire convergentie voor polyeders is aangetoond, maar de constante hangt van de geometrie af; bij een lange keten met strakke einden kan het veel sweeps kosten (stalling-resultaat). Daarom: warm start vanaf clamp(z, L, U) en begrens het aantal sweeps.
- De AMLE/midrange-iteratie is het juiste gereedschap als er géén terreindoel is (bv. een brugdek dat "zo vlak mogelijk" tussen vaste einden moet hangen): het geeft de uitbreiding met de kleinste maximale helling, lexicografisch, en is stabiel (Cor. 3.7). Met een terreindoel is het verkeerd: het trekt de weg naar het rechte vlak tussen vaste knopen, los van het maaiveld.
- Een combinatie die beide dient (eigen voorstel): (1) haalbaarheid en omhullenden via Dijkstra; (2) h⁰ = clamp(z, L, U); (3) Dykstra-sweeps op de schakelbeperkingen tot tolerantie. Omdat alleen de nieuwe weg (≈30 knopen) vrij is en de rest vast, zijn er ~60 beperkingen; honderd sweeps zijn ~6000 projecties, verwaarloosbaar t.o.v. 0,2 s.

### Gaps
- Geen bron gevonden met een expliciete convergentiesnelheid van Dykstra of POCS specifiek voor verschilbeperkingen op een pad of graaf; het "lineair voor polyeders" is algemeen.
- Geen gepubliceerde vergelijking gevonden tussen midrange/AMLE-iteratie en kleinste-kwadraten met hellingsgrens voor wegprofielen.

---

## Key Question 4: Onhaalbaarheid (twee vaste knopen te dicht bij elkaar) — praktijk en zachte formuleringen

### Takeaway
De vakliteratuur en software detecteren onhaalbaarheid via de hellingen tussen vaste punten (kortste pad) vóór het optimaliseren, en reageren door randvoorwaarden te "verzachten" (tolerantie op de helling bij een controlepunt, bredere band) met de uitdrukkelijke kanttekening dat de uitkomst dan niet optimaal is; ontwerprichtlijnen laten plaatselijk steilere hellingen toe (tot 15% in zeer beperkte omstandigheden, +1% voor korte eenrichtingsafdalingen). Geen bron geeft een regel welke vaste knoop opgegeven moet worden; dat blijft een ontwerpkeuze.

### Cited Findings
- Softree Optimal: een onhaalbaar probleem meldt alleen "No Profile could be found"; controlepunten buiten de verticale band zijn een basisconflict; aanpak: beginnen met minimale randvoorwaarden (grote band, grote hellingen, kleine bogen, geen controlepunten) en ze stapsgewijs toevoegen met een Feasibility-check; "softening" ontspant een geometrische randvoorwaarde zonder de parameters te wijzigen, de overschrijdingen worden in het log gemeld, en "an alignment found with a softened constraint should not be considered optimal"; geen volgorde van verzachten gegeven — [Softree: Infeasible problems](https://support.softree.com/knowledge-base/optimization/softree-optimal-concepts/infeasible-problems); foutmeldingen: hellingsovertreding bij een controlepunt → tolerantie op de helling toevoegen/vergroten; bandovertreding → band vergroten — [Softree RoadEng opterror](https://www.softree.com/help/roadeng11/opterror.htm); verticale optimalisatie wordt afgeraden bij vaste secties — [Softree: Vertical alignment optimization](https://support.softree.com/knowledge-base/optimization/vertical-alignment-optimization)
- Patent US 6,996,507: bij twee of meer vaste punten moeten de hellingen tussen de vaste punten vóór de optimalisatie worden gecontroleerd via het kortste pad ertussen — [USPTO 6996507](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/6996507) (zoekfragment)
- Autodesk Civil 3D Grading Optimization meldt "violations" en een convergentieplot; randvoorwaarden kunnen conflicteren — [Autodesk: Optimization Constraints](https://help.autodesk.com/cloudhelp/ENU/C3DGO-Tutorials/files/tutorials/constraints.html); [Convergence plot and violations](https://help.autodesk.com/cloudhelp/ENU/C3DGO-Tutorials/files/tutorials/conv_plot_and_violations.html)
- Monnet, Hare & Lucet 2020: alleen een paar randvoorwaarden van de MILP bepalen haalbaarheid; snelle toets 75× sneller dan de volledige MILP — [Springer](https://link.springer.com/article/10.1007/s10589-019-00160-3)
- Ontwerpruimte in richtlijnen (TxDOT Roadway Design Manual, tabel 4-11, op AASHTO gebaseerd): "up to 15% can be used in very constrained conditions"; maximale hellingen mogen met 1% verhoogd worden voor eenrichtingsafdalingen korter dan 500 ft zonder design exception; bij >10% zwaar vrachtverkeer hooguit 5% (vlak) / 6% (heuvelachtig) — [TxDOT RDM 4.8.1 Grades](https://www.txdot.gov/manuals/des/rdw/chapter-4--basic-design-criteria/4-8-vertical-alignment/4-8-1-grades.html)
- Kyng et al. Lemma 3.5 / SteepestPath: het steilste terminalpad (het paar vaste knopen dat de haalbaarheid breekt) is in verwachte O(m + n log n) te vinden; Lemma 4.3 geeft per knoop vHigh > vLow als het haalbare interval leeg is — [arXiv 1505.00290](https://ar5iv.labs.arxiv.org/html/1505.00290)

### Inferences
- Diagnose (eigen afleiding uit vraag 2): bij L(x) > U(x) leveren de argmin/argmax-terminals s (voor U) en t (voor L) het conflicterende paar; de vereiste minimale helling is g_req = |h_s − h_t| / dist(s,t). Dat getal is meteen het beslissingscriterium.
- Beslisboom (eigen voorstel, gebaseerd op de ontwerpruimte in de richtlijnen en de Softree-praktijk):
  1. g_req ≤ g_abs (absolute maximum, bv. 12–15% volgens TxDOT in beperkte omstandigheden): accepteer plaatselijk de steilere helling alleen op het pad s–t (verhoog g voor die schakels), toon het als waarschuwing (Softree-stijl "softened constraint").
  2. g_req > g_abs: kies tussen (a) een kunstwerk: maak van de nieuwe weg een brug/tunnel ten opzichte van de vaste knoop (de vaste knoop wordt vrij voor de weg maar blijft voor het terrein; dit is de "ongelijkvloers"-oplossing die het spel al kent), (b) de nieuwste vaste knoop (de aansluiting die de speler net legt) laten vieren: bestaande wegen nooit verzetten (regel in CLAUDE.md), dus het nieuwe eind krijgt een eigen niveau en een brug/onderdoorgang, (c) omleiden/langer maken: de helling is Δh/lengte, dus een omweg of een kop verlengt het pad (serpentine), (d) de aansluiting weigeren met de reden (zoals `CanPlace`/`CanStart` met rode vlag).
- Zachte formulering (eigen voorstel): min Σ w_i (h_i − z_i)² + λ Σ_{(i,j)} max(0, |h_i − h_j| − g·d_ij)² met vaste knopen hard; convex en glad op stuksgewijze wijze, oplosbaar met geprojecteerde gradiënt of Gauss-Seidel per knoop; λ groot geeft bijna-harde hellingen, en bij onhaalbaarheid verdeelt de straf de overschrijding over de schakels van het conflictpad in plaats van te divergeren. Nadeel: twee gewichten te kiezen; voordeel: nooit "geen oplossing". Dit volgt de geest van Softree's "softening" (overschrijding toegestaan, gerapporteerd).
- Welke vaste knoop opgeven: geen literatuur gevonden; in het spel is de logische voorrang: bestaande wegen > brugdekken/portalen (vaste maat) > nieuwe aansluitpunten; bij conflict tussen twee bestaande vaste knopen (ontstaan door een eerder gebouwd stuk) is de weg zelf onhaalbaar en moet het leggen geweigerd of ongelijkvloers gemaakt worden.

### Gaps
- Geen bron die een rangorde van "op te geven" vaste punten voorschrijft, noch een vakpublicatie met een penalty-formulering (hellingsovertreding vs. grondverzet) voor wegprofielen.
- De patenttekst is alleen via een zoekfragment bekend; de gescande PDF was niet leesbaar.

---

## Key Question 5: Eenvoud — kan het als klein QP/LP zonder afhankelijkheden in C#? Aanbeveling

### Takeaway
Ja. Het probleem zonder boogstralen en grondtransport is een convex QP met alleen verschil- en doosbeperkingen; twee Dijkstra-runs geven haalbaarheid en de strakste grenzen in O(E log V), en een Dykstra-projectielus (enkele regels C#, geen bibliotheek) op alleen de vrije knopen van de nieuwe weg geeft de kleinste-kwadratenoplossing. Voor 30 vrije knopen is dat ver onder een milliseconde; de volledige herberekening van 5000 knopen is ook haalbaar maar zelden nodig.

### Cited Findings
- Haalbaarheid/omhullenden: multi-source kortste paden, O(m + n log n) (Kyng et al. Theorem 4.6; CLRS 24.4) — [arXiv 1505.00290](https://ar5iv.labs.arxiv.org/html/1505.00290); [walkccc CLRS 24.4](https://walkccc.me/CLRS/Chap24/24.4/)
- Dykstra convergeert naar de projectie op de doorsnede van convexe verzamelingen; POCS niet — [Wikipedia Dykstra](https://en.wikipedia.org/wiki/Dykstra%27s_projection_algorithm); lineaire convergentie voor polyeders — [Bauschke et al.](https://digital.library.adelaide.edu.au/items/2d614926-3c1d-4fa2-a5be-5ddffe4b40a9)
- Lipschitz-kleinste-kwadraten als QP: standaard (simest via NNLS; Mazumder et al. via augmented Lagrangian voor grote n) — [CRAN simest](https://rdrr.io/cran/simest/man/cvx.lip.reg.html); [Mazumder et al.](https://dspace.mit.edu/handle/1721.1/120818)
- AMLE als terreinloos alternatief: midrange-iteratie convergeert (Bačák et al. Cor. 5.6), exact in Õ(mn) (Kyng et al.) — [arXiv 1903.04873](https://ar5iv.labs.arxiv.org/html/1903.04873); [arXiv 1505.00290](https://ar5iv.labs.arxiv.org/html/1505.00290)
- Civiele MILP-modellen kosten seconden tot minuten zelfs convex (CUVA 1,1–61 s) — [arXiv 2508.15953](https://arxiv.org/html/2508.15953v1)

### Inferences
- Aanbevolen pijplijn (eigen voorstel, alle stappen op gesourcete bouwstenen):
  ```
  Input: graaf (knopen van de nieuwe weg + aansluitknopen), z_i terrein, g, vaste T met h_t.
  1. Omhullenden: U = Dijkstra(+h_t), L = -Dijkstra(-h_t), kosten g*d_ij.   // O(E log V)
     Als max(L-U) > eps: onhaalbaar -> conflictpaar (s,t), g_req = |h_s-h_t|/dist(s,t) -> vraag 4.
  2. Start: h_i = clamp(z_i, L_i, U_i) voor vrije i; h_t vast.
  3. Dykstra-sweeps over alle schakels (zie vraag 3) tot max overschrijding < 1 mm of N sweeps.
     Doosgrenzen [L,U] mogen als extra projecties mee (ook halfvlakken), maar zijn impliciet al
     gevolg van de schakelbeperkingen + vaste knopen; ze versnellen alleen.
  4. Optioneel gewicht w_i (bv. bebouwd vak zwaarder, kruispunt zwaarder): schaal h en z met sqrt(w_i)
     per knoop; de projectie op h_i - h_j <= c wordt dan gewogen (verschuiving omgekeerd evenredig met w).
  5. Incrementeel: alleen de knopen van de nieuwe weg zijn vrij; alle bestaande wegknopen zijn T.
     Daardoor is V_vrij ≈ 30 en zijn stappen 1-3 microseconden. Kruispunten (graad >= 3) zijn gewoon
     knopen: een nieuw kruispunt op een bestaande weg is vast (h van die weg); een nieuw kruispunt
     tussen twee nieuwe wegen is vrij met meer schakels.
  ```
- Waarom niet een LP/QP-solver schrijven: een generieke actieve-set- of inwendige-puntmethode is meer code en foutgevoeliger dan ~40 regels Dijkstra + Dykstra, en levert dezelfde oplossing (uniek, want het QP is strikt convex in h).
- Waarom niet alleen "Dijkstra-omhullenden + clamp": clamp(z, L, U) is niet Lipschitz waar z zelf steiler is dan g (eigen observatie uit de "niet elke functie tussen m en M"-uitspraak); de smoothing-stap is nodig. De AMLE/midrange-iteratie is wél een geldige (en terreinloze) smoothing, geschikt voor brugdekken en kokers; voor gewone wegen is de kleinste-kwadratenprojectie passender omdat ze de weg op het maaiveld houdt.
- Waarom de huidige halveer-lus wel bijna goed is: het is POCS; toevoegen van Dykstra's correctiescalar per beperking maakt er een echte projectie van, en de Dijkstra-toets vooraf voorkomt dat de lus bij onhaalbaarheid eindeloos oscilleert.
- Robuustheid: Dijkstra is exact; Dykstra convergeert monotoon-dualistisch, maar kan traag zijn bij lange ketens tussen twee strakke vaste einden (stalling). Praktische vangnetten: warm start uit clamp, sweep-limiet (bv. 200), en een eindcontrole: als na de limiet nog overschrijding rest, snap die schakels op de grens (POCS-stap) — dan is het haalbaar maar niet exact optimaal, wat het spel niet merkt.

### Gaps
- Geen gemeten C#-benchmarks gevonden voor dit specifieke probleem; de tijdschattingen (<1 ms) zijn eigen inschatting op basis van de complexiteit en de probleemgrootte.

---

## Key Question 6: Praktijkdrempels: talud/ingraving vs. keermuur, aardebaan vs. viaduct, ingraving vs. tunnel; maximale hellingen in richtlijnen

### Takeaway
Er bestaat geen universele, gesourcete kostendrempel in meters; de gevonden praktijkcijfers zijn: aardebanen tot ca. 9 m, daarboven viaduct overwegen (JICA, spoor Vietnam), bermen in taluds vanaf 6 m hoogte, een gewapend-grond-wand ca. 40% goedkoper dan een viaduct, en cut-and-cover (open bouwput) voordeliger dan boortunnel tot ca. 20 m diepte. Maximale hellingen: AASHTO/TxDOT 3–5% snelwegen tot 8–12% lokale wegen (15% in zeer beperkte omstandigheden); Nederlandse NOA ca. 3% bij 120 km/h, 4–5% bij 100, 4–6% bij 80 (secundaire bron).

### Cited Findings
- JICA-studie spoorlijn Vietnam: aardebaanhoogte op gewone secties ca. 9 m; viaducten bouwen als de hoogte meer dan 9 m is; standaardhoogte voor bermen in hoge taluds boven 6,0 m — [JICA 12345716_02 (PDF)](https://openjicareport.jica.go.jp/pdf/12345716_02.pdf)
- JICA-studie (ander spoorproject): met staal gewapende grond met betonpanelen is ca. 40% goedkoper dan een viaduct — [JICA 12088381_05 (PDF)](https://openjicareport.jica.go.jp/pdf/12088381_05.pdf)
- Aardebanen nemen veel grond in; in voorstedelijk gebied kan een aardebaan daardoor duurder zijn dan een viaduct; keuze als afweging van economie, veiligheid, milieu en bouwtijd — [DergiPark artikel (PDF)](https://dergipark.org.tr/en/download/article-file/3479075)
- Massieve aardebanen zijn vaak een goedkope oplossing om elders vrijkomende grond te gebruiken; een viaduct kan toch de voorkeur krijgen bij waardevolle ecosystemen — [IENE Handbook 5.5.6](https://www.handbook.iene.info/handbook/5-solutions/5-5-wildlife-passages/5-5-6-adapted-viaducts-landscape-underpasses/)
- Los Angeles Complete Street Design Manual: de kosten van stabiliseren, beplanten en eeuwig onderhoud van een steil talud kunnen de kosten van grondwerk en grondverwerving voor een flauwer talud overtreffen; vergelijking vlak vs. steil "very difficult to evaluate" — [LA CSDM E-683](https://completestreetdesignmanual.engineering.lacity.gov/e-600-cross-section-elements/e-680-side-slopes/e-683-economic-considerations)
- Contractclausule (VS): vereist een vergelijkende kostenanalyse van keermuurtypen tegenover aardebaan, grondstabilisatie en beschikbare ROW vóór het muurontwerp; talud 4:1 of flauwer tot het muurvlak — [Law Insider clausule](https://www.lawinsider.com/clause/retaining-walls-and-miscellaneous-structures/_1)
- Cut-and-cover is de beste optie bij een relatief ondiepe tunnel (max. diepte < 20 m) en een vrij maaiveld; kosten stijgen snel met de diepte; diepwanden boven ca. 20 m vereisen trench-cutters — [EU SCOUT project (PDF)](https://trimis.ec.europa.eu/system/files/project/documents/20120503_124336_24683_SCOUT%201.%20Project.pdf); [Polito trench cutters (PDF)](https://iris.polito.it/retrieve/handle/11583/2509084/60076/2509084_Trench%20cutters2013.pdf); vergelijkende studie: onderheiing, nutsverlegging en herstel bij cut-and-cover kunnen de kale tunnelbouwkosten overstijgen — [TRID 143946](https://trid.trb.org/view/143946)
- Terreinklassen AASHTO Green Book 2018: vlak < 2%, heuvelachtig 2–8%, bergachtig > 8% — [TxDOT RDM 4.8.1](https://www.txdot.gov/manuals/des/rdw/chapter-4--basic-design-criteria/4-8-vertical-alignment/4-8-1-grades.html) (via zoekresultaat op AASHTO-tabel); WSDOT Design Manual, landelijk: vlak 5% (25–40 mph) tot 3% (60–80 mph); heuvelachtig 8% (25 mph) tot 4% (60–80); bergachtig 10% (25) tot 5% (65–80); stedelijk bergachtig 20 mph 13% — [WSDOT M22-01 1220 (PDF)](https://wsdot.wa.gov/publications/manuals/fulltext/M22-01/1220.pdf) (uit zoekfragment; PDF niet gelezen)
- TxDOT tabel 4-11 (maximale helling %, oplopende ontwerpsnelheden): stedelijk lokaal 8% overal; stedelijk collector vlak 9→6, heuvelachtig 12→7; stedelijk arterial vlak 8→5, heuvelachtig 9→6; freeway vlak 4→3, heuvelachtig 5→4; landelijk lokaal vlak 9→5, heuvelachtig 12→6; landelijk collector vlak 7→5, heuvelachtig 10→6; landelijk arterial vlak 5→3, heuvelachtig 6→4. Minimale helling 0,3% bij trottoirbanden voor afwatering; onverharde greppels ≥ 0,5%, beklede ≥ 0,25% — [TxDOT RDM 4.8.1 Grades](https://www.txdot.gov/manuals/des/rdw/chapter-4--basic-design-criteria/4-8-vertical-alignment/4-8-1-grades.html)
- Nederland (secundaire bronnen): NOA-tabel maximale hellingspercentages per ontwerpsnelheid: ca. 3% bij 120 km/h, 4–5% bij 100 km/h, 4–6% bij 80 km/h — [TU Delft tabellenblad (PDF, gescand)](https://ocw.tudelft.nl/wp-content/uploads/Tabellenblad-tentamen.pdf) (uit zoekfragment); EIB-ontwerpblad met ROA/NOA-rijen: "maximale helling verticale rechtstand 3,0% 3,0% 4,0% 4,0% 7,0% 8,0%" en 5–6% bij grote kunstwerken (kolommen niet aan wegcategorieën te koppelen) — [EIB register 131845149 (PDF)](https://www.eib.org/attachments/registers/131845149.pdf); gemeentelijk plan hanteert 5% maximum voor een lokale weg — [ruimtelijkeplannen.nl](https://www.ruimtelijkeplannen.nl/documents/NL.IMRO.0141.BP00030-0601/tb_NL.IMRO.0141.BP00030-0601_81.pdf); Handboek Wegontwerp-delen (CROW) bestaan per wegcategorie — [SWOV: HWO gebiedsontsluitingswegen](https://swov.nl/en/publicatie/handboek-wegontwerp-wegen-buiten-de-bebouwde-kom-gebiedsontsluitingswegen)
- VK: DMRB CD 109 Revision 1 bevat de secties over hellingen en voetbogen; concrete percentages niet uit de resultaten te halen; een A90-studie noemt 4% maximum voor een optie — [North Herts CD 109 extract](https://new.north-herts.gov.uk/sites/default/files/2023-07/CD113%20-%20DMRB%20CD%20109%20revision%201%20Extract.pdf); Kirklees-richtlijn deelt hellingen in banden 5–8%, 8–10% en ≥10% met mitigatie — [Kirklees gradients (PDF)](https://www.kirklees.gov.uk/beta/regeneration-and-development/pdf/highways-guidance-gradients.pdf)

### Inferences
- Voor het spel (1 eenheid = 1 m, taluds 1:1,5) zijn bruikbare, aan de bronnen ontleende maar niet letterlijk gesourcete drempels: talud tot ca. 6 m zonder berm, 6–9 m talud met berm of keermuur, boven ca. 9 m hoogteverschil een viaduct/brug; ingraving tot enkele meters open, vanaf ca. 2,25–9 m een onderdoorgang/koker (de bestaande spelregels: tunnel vanaf 9 m berg, 6 m met U), een open bouwput/tunnel zinvol tot ca. 20 m, daarboven "echte" tunnel. De 20 m-grens komt uit de tunnelbouw en zegt niets over de helling van de toerit.
- Een werkbare maximale helling voor stadsstraten in het spel is 8–10% (TxDOT lokaal 8%, collector tot 12%), met een absoluut maximum 15% voor korte stukken; voor brede wegen/lanen 5–6%.

### Gaps
- Geen gesourcete "X m: keermuur goedkoper dan talud" of "Y m: viaduct goedkoper dan aardebaan" uit AASHTO, Rijkswaterstaat, CROW of DMRB; alleen de JICA-cijfers (9 m, 6 m, 40%) en de 20 m cut-and-cover-grens.
- De CROW Handboek Wegontwerp- en ASVV-tabellen zelf zijn niet toegankelijk; de Nederlandse percentages komen uit cursusmateriaal en een EIB-blad met onleesbare kolomkoppen.
- De WSDOT- en TU Delft-PDF's konden niet gerenderd worden (geen pdftoppm); waarden stammen uit zoekfragmenten.
