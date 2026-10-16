using System.Text.RegularExpressions;
using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Curated aesthetic vocabulary for references that a generic color lexicon cannot understand:
/// named internet aesthetics, visual movements, pop-culture shorthand, and Pinterest's 2026 trend
/// names. Profiles are art-directed starting points; prompt modifiers are layered onto the profile,
/// then PaletteQualityValidator makes the result readable before it reaches any UI.
/// </summary>
public static class AestheticProfileCatalog
{
    public sealed record Profile(
        string Id,
        string Name,
        string Description,
        string[] Aliases,
        string BackgroundBase,
        string BackgroundAlt,
        string Surface,
        string AccentPrimary,
        string AccentStrong,
        string TextPrimary,
        string TextMuted,
        string BorderSubtle);

    private static readonly Profile[] AllProfiles =
    [
        new("neon-afterglow", "Neon Afterglow", "A nocturnal synthwave palette: ink-dark surfaces, ultraviolet depth, hot-magenta glow and a clean cyan counterpoint.",
            ["neon afterglow", "neon nightlife", "neon tokyo", "cyberpunk", "cyberpunk 2077", "night city", "synthwave city"],
            "#0B0616", "#160D27", "#24113A", "#FF4FD8", "#77F7FF", "#F7EFFF", "#C8B8D8", "#4A2A63"),
        new("vaporwave", "Vaporwave Dream", "A nostalgic digital sunset in deep violet, electric pink, pool cyan and soft lilac.",
            ["vaporwave", "vapor wave", "aesthetic 80s", "retrowave", "retro wave", "synthwave", "outrun"],
            "#110B2B", "#211443", "#34205C", "#FF71CE", "#01CDFE", "#FFF0FC", "#C9B8F1", "#513B78"),
        new("frutiger-aero", "Frutiger Aero", "Glossy Y2K optimism: sky blue, liquid aqua, fresh whites and crisp oceanic accents.",
            ["frutiger aero", "aero", "windows vista aesthetic", "glossy aqua", "liquid glass aesthetic"],
            "#EAFBFF", "#CDEEF5", "#B5DFE8", "#087FCA", "#07566F", "#123747", "#416B7A", "#93C8D6"),
        new("y2k-chrome", "Chrome Dream", "Y2K futurism with polished silver-lilac, icy highlights and sharp violet hardware accents.",
            ["y2k", "y2k aesthetic", "chrome dream", "cyber y2k", "2000s futurism", "liquid chrome"],
            "#F3F1FC", "#DAD6EC", "#C1BBD9", "#5945A8", "#2F285F", "#211D35", "#5C5678", "#AAA3CD"),
        new("dark-academia", "Dark Academia", "Ink, old-library umber, worn leather and antique brass: cinematic, literate and warm without going muddy.",
            ["dark academia", "dark academic", "gothic academia", "old library", "old money library"],
            "#17191D", "#23262A", "#302D2C", "#B68A4A", "#E1C48D", "#F5EEE0", "#C9BDA7", "#555052"),
        new("light-academia", "Light Academia", "Sunlit paper, pale stone, quiet olive and warm archival ink with a soft scholarly glow.",
            ["light academia", "light academic", "romantic academia", "sunlit library"],
            "#F6F0E5", "#E8DDCB", "#D8C8AE", "#697A50", "#8D5C40", "#302A25", "#655A4F", "#C6B69C"),
        new("poetcore", "Poetcore", "Notebook cream, ink-blue, dried-rose leather and fountain-pen contrast for a quiet protagonist desk.",
            ["poetcore", "poet core", "poet aesthetic", "writercore", "writer aesthetic", "booktok desk", "poet desk", "poetry notebook", "fountain pen desk", "bookish study", "satchel and tie aesthetic", "lived in library"],
            "#F4E9D8", "#E6D4BE", "#D4BDA2", "#824E43", "#304453", "#302923", "#65574B", "#C6B49C"),
        new("pen-pals", "Pen Pals", "A tactile letter-writing desk: stationery cream, fountain-pen navy, postage red and a quiet botanical green.",
            ["pen pals", "pen pals trend", "letter writing aesthetic", "snail mail", "stationery aesthetic", "handwritten letters", "cute stamps", "letter-writing renaissance", "postcard aesthetic", "pen pal letters", "letter writing desk", "postage stamp collection", "snail mail kit"],
            "#F8F0E2", "#E8DFC9", "#D6C8AB", "#A63D45", "#294B63", "#282B30", "#5C6264", "#C5B69C"),
        new("cottagecore", "Cottagecore", "Meadow sage, weathered cream and botanical greens: handmade, natural and softly sunlit.",
            ["cottagecore", "forestcore", "naturecore", "grandmacore", "cozy cottage", "cottage garden"],
            "#F0F3E6", "#DEE8D2", "#C5D5B8", "#4F7550", "#31543D", "#263529", "#5C6D5B", "#B5C5A9"),
        new("wilderkind", "Wilderkind", "A storybook woodland palette of fern, fawn, moth-wing cream and restrained berry accents.",
            ["wilderkind", "wildkind", "deer aesthetic", "forest fairy", "fairycore", "mushroomcore", "enchanted forest", "woodland creature", "moth wing aesthetic", "forest spirit", "nature fairy aesthetic", "animal fairycore"],
            "#F0F4E8", "#DCE8D0", "#C8D9B7", "#4F795D", "#A65B62", "#27372D", "#5D705F", "#B9CBB0"),
        new("vamp-romantic", "Vamp Romantic", "Romantic goth after dark: black-cherry shadows, oxblood, rose satin and candlelit ivory.",
            ["vamp romantic", "vamp romantic trend", "romantic goth", "gothic romance", "vampire aesthetic", "vampirecore", "carmilla aesthetic", "jet black nails", "dark romantic makeup", "black cherry goth", "romantic vampire", "candlelit goth"],
            "#100910", "#21121F", "#321828", "#C32968", "#E6A8C4", "#FFF2F5", "#D1AEC0", "#593047"),
        new("neo-deco", "Neo Deco", "A modern Art Deco treatment: midnight plum, sculpted brass, lacquered surfaces and geometric glamour.",
            ["neo deco", "neo-deco", "art deco", "modern art deco", "deco glam", "brass and black", "geometric glamour", "sculpted brass", "fan motif decor", "black and gold art deco", "deco revival"],
            "#15131C", "#24212E", "#332E3E", "#C59A55", "#F0D39A", "#F8F2E9", "#C7B9C7", "#5B4C61"),
        new("extra-celestial", "Extra Celestial", "Opalescent ice, deep orbital blue, lunar lavender and atmospheric cyan from a polished sci-fi future.",
            ["extra celestial", "extra-celestial", "spacecore", "cosmic aesthetic", "alien core", "galaxy princess", "interstellar", "celestial aesthetic", "alien beauty", "space age beauty", "opalescent orbit", "iridescent sci fi", "celestial runway"],
            "#081126", "#14264A", "#20385D", "#7DDFF5", "#BBA2FF", "#F3F7FF", "#B9C9E6", "#3D537B"),
        new("glamoratti", "Glamoratti", "Maximalist 80s luxury: lacquered aubergine, champagne metal and confident high-gloss highlights.",
            ["glamoratti", "glamoratti trend", "80s luxury", "maximalist luxury", "power dressing aesthetic", "chunky belt", "baggy suit", "high collar jacket", "gold cuff", "80s power dressing", "maximalist corporate glamour"],
            "#171019", "#2B1C2C", "#403048", "#C99B52", "#F0D27C", "#FFF5E9", "#D4BED0", "#684B69"),
        new("opera-aesthetic", "Opera Aesthetic", "Velvet curtains, theatrical wine red, antique gold and candlelit ivory with dramatic depth.",
            ["opera aesthetic", "opera aesthetic trend", "dark cabaret", "midnight masquerade", "masquerade aesthetic", "theatrical romantic", "theatrical ball", "masquerade ball", "opera gloves", "velvet curtain", "black tie drama"],
            "#170B17", "#2C1027", "#451A35", "#A92A50", "#E6B46B", "#FFF0E3", "#D8B7C3", "#704052"),
        new("mystic-outlands", "Mystic Outlands", "An otherworldly landscape of lichen, mossed stone, stormy teal and mineral gold.",
            ["mystic outlands", "mystic outlands trend", "fantasy landscape", "mythic wilderness", "dark fairy forest", "scotland highlands aesthetic", "faroe islands aesthetic", "ethereal landscapes", "misty ruins", "enchanted wilderness", "fantasy travel", "otherworldly landscape"],
            "#101B22", "#1E2C2C", "#2B3D3A", "#69B798", "#D6BC72", "#F2F5E8", "#B9CBBB", "#455D55"),
        new("cool-blue", "Cool Blue", "Glacier blue, porcelain, mist and marine ink: fresh, precise and quietly futuristic.",
            ["cool blue", "cool blue trend", "pinterest palette cool blue", "cool blue palette 2026", "cool wasabi", "cool-wasabi palette", "glacier aesthetic", "ice blue aesthetic", "blue hour", "icy minimalism", "frosted makeup", "subzero aesthetic"],
            "#EAF4FF", "#D1E5F8", "#B8D4EB", "#286DB5", "#17436F", "#172F46", "#4E6B87", "#A4C4E0"),
        new("gimme-gummy", "Gimme Gummy", "Jelly-bright color with candy-glass pink, grape, creamy vanilla and playful translucent accents.",
            ["gimme gummy", "gimme gummy trend", "gummy aesthetic", "jellycore", "candycore", "gummy bear aesthetic", "jelly candy", "jelly blush", "gummy bear accessories", "rubberized accessories", "candy glass", "bendy phone case", "3d gummy jewelry"],
            "#FFF3FA", "#F4DDF0", "#E5CAF1", "#C83E96", "#7047BF", "#351F3B", "#714E77", "#DFB9D9"),
        new("glitchy-glam", "Glitchy Glam", "Digital-night glamour: ink, ultraviolet, laser pink and mint-cyan interference with a polished finish.",
            ["glitchy glam", "glitchy glam trend", "glitchcore", "glitch aesthetic", "cyber glam", "digital rave", "digital rave glam", "chromatic glitch", "glitch makeup", "cyber glam beauty"],
            "#0E0C1B", "#21172C", "#33213F", "#F653A3", "#43E1CB", "#FFF0FA", "#C9B3D8", "#563260"),
        new("afrohemian", "Afrohemian Decor", "Earth pigments, woven umber, ochre, terracotta and grounded botanical warmth.",
            ["afrohemian", "afrohemian decor", "afro bohemian", "boho maximalist", "earthy maximalism", "afrohemian home decor", "woven earth tones", "natural fibers decor", "terracotta botanicals"],
            "#F4EBD8", "#E4D2B4", "#D3B88E", "#9B4B31", "#4D6B45", "#30271E", "#655545", "#BDA47C"),
        new("khaki-coded", "Khaki Coded", "Field-kit neutrals with olive, weathered stone, canvas and a practical deep-forest anchor.",
            ["khaki coded", "khaki-coded", "gorpcore", "utility aesthetic", "field jacket aesthetic", "expedition core", "utility fashion", "canvas field kit", "outdoor technical wear", "heritage fieldwear"],
            "#F0EDDF", "#DCD6C0", "#C5C2A7", "#536B4D", "#465C70", "#282F2A", "#5C6558", "#B8B49B"),
        new("funhaus", "FunHaus", "Playful Bauhaus energy: cream, lilac, periwinkle and fearless coral-and-cobalt color blocking.",
            ["funhaus", "funhaus trend", "playful bauhaus", "color block aesthetic", "memphis design", "circuscore decor", "playful circus interior", "bold color blocking", "memphis revival", "whimsical maximalist decor"],
            "#FBF2E8", "#EBD7E8", "#CCD8F0", "#D93661", "#403BB5", "#28223D", "#5F507A", "#DAB9CE"),
        new("laced-up", "Laced Up", "Modern lace and soft romantic detail translated into porcelain, mauve, rosewood and plum.",
            ["laced up", "laced-up", "laced up trend", "lacecore", "lace aesthetic", "romantic balletcore", "lace gloves", "ribboncore", "romantic lace", "ballet ribbons", "lace and satin"],
            "#FFF7F9", "#F1E1E9", "#DCCBDE", "#A84C79", "#574071", "#342738", "#735A73", "#E0C9D7"),
        new("throwback-kid", "Throwback Kid", "Playful nostalgia: soft candy colors, retro toy-box blue and raspberry with clean readable ink.",
            ["throwback kid", "throwback kid trend", "kidcore", "90s nostalgia", "nostalgia core", "retro toy aesthetic", "retro toybox", "colorful nostalgic decor", "90s toy aesthetic", "childhood bedroom nostalgia"],
            "#FFF5EC", "#F7DCE4", "#D6EAF3", "#D74673", "#337EA6", "#30243A", "#69596C", "#E4BCC9"),
        new("barbiecore", "Barbiecore", "High-fashion pink with glossy rose, fuchsia and violet grounding; bold, playful and deliberately unapologetic.",
            ["barbiecore", "barbie core", "pink glamour", "hot pink aesthetic", "pink maximalism"],
            "#FFF0F7", "#FFD3E7", "#F2B8D8", "#D90086", "#7828A0", "#3B1838", "#7C476D", "#E9A5CC"),
        new("arcane-inspired", "Arcane-Inspired", "Painterly Zaun-and-Piltover contrast: bruised violet, petrol blue, brass firelight and pale enamel.",
            ["arcane", "arcane series", "arcane league of legends", "zaun", "piltover", "hextech"],
            "#111126", "#20213D", "#30314F", "#8861CC", "#F0B34D", "#F4F1FF", "#BDB9E0", "#4B4A72"),
        new("ghibli-inspired", "Storybook Forest", "Hand-painted storybook greens, airy sky, warm paper and moss-dark anchors; whimsical without muddy colors.",
            ["studio ghibli", "ghibli", "totoro", "howls moving castle", "spirited away", "storybook anime", "ghibli forest"],
            "#EDF5E8", "#D1E4CD", "#B8D4B5", "#3F795D", "#2F5B68", "#26372D", "#5C705E", "#AAC8AF"),
        new("zelda-inspired", "Hyrule Forest", "Adventure-fantasy forest green, ancient gold, moss light and crisp sky-blue relic accents.",
            ["zelda", "legend of zelda", "hyrule", "korok forest", "zelda forest"],
            "#10231C", "#1A392B", "#28503B", "#D7B653", "#8DDAA5", "#F2F5E7", "#BDCCB0", "#456653"),
        new("wednesday-goth", "Wednesday Goth", "Black-plum, graphite, smoky lavender and cold silver: sharp, gothic and deliberately restrained.",
            ["wednesday addams", "wednesday", "addams family", "nevermore academy", "goth girl aesthetic"],
            "#100F17", "#1E1B29", "#2D293B", "#9C83C8", "#D4CADF", "#F4F0F8", "#B7AEC4", "#50485E"),
        new("cherry-grove", "Cherry Grove", "Minecraft-inspired cherry blossoms: pixel-soft pink, moss, sky haze and earthy block tones.",
            ["minecraft cherry grove", "cherry grove", "minecraft cherry blossom", "minecraft", "pink forest pixel"],
            "#F8F0F5", "#EAD8E5", "#C8D7C1", "#C85C91", "#5E8063", "#302B35", "#6E626E", "#D7B8CC"),
        new("space-opera", "Space Opera", "Deep orbital navy, starship cyan, warm starlight and a disciplined cinematic control-room palette.",
            ["space opera", "star wars", "galactic empire", "jedi", "sith", "starship cockpit", "interstellar cockpit"],
            "#09111F", "#17263B", "#22364F", "#F2C14E", "#65D2EE", "#F3F7FC", "#B7C6D8", "#425A73"),
        new("kpop-stage", "K-Pop Stage", "A concert-light palette of glossy magenta, electric blue, cool black and a pearl highlight.",
            ["kpop", "k-pop", "k pop comeback", "idol stage", "k-pop stage", "album concept", "comeback concept"],
            "#100B1B", "#251332", "#38204A", "#F34CA7", "#47B9FF", "#FFF2FB", "#CBB7D8", "#57356B"),
        new("fallout-terminal", "Retro-Future Terminal", "Analog green phosphor, charcoal glass and amber warning lights on a retro-futurist terminal.",
            ["fallout", "pip boy", "pip-boy", "retro terminal", "green phosphor", "terminal hacker", "hacker terminal"],
            "#0C1510", "#17271B", "#24392A", "#8FE388", "#F0C568", "#F0F7E9", "#B7CCAE", "#3E5B44"),
        new("blade-runner", "Rain City 2049", "Rain-slick petrol blue with sodium amber, smoky violet and cinematic night contrast.",
            ["blade runner", "blade runner 2049", "rain city 2049", "neo noir city", "neo-noir", "orange teal cinematic"],
            "#0B111B", "#151F2D", "#223040", "#FFAA52", "#57D4D0", "#F4F6FA", "#B7C4D2", "#40566B"),
        new("tron", "Digital Grid", "Black glass and precise cyan light lines, with vivid blue reserved for interactive highlights.",
            ["tron", "tron legacy", "digital grid", "laser grid", "blue cyber grid"],
            "#050C14", "#0A1925", "#112A39", "#43E8FF", "#3C8DFF", "#F1FCFF", "#ADD8E3", "#245064"),
        new("city-pop", "City Pop Afterhours", "Japanese city-pop warmth: sunset coral, poolside cyan, night-indigo and a nostalgic cream highlight.",
            ["city pop", "japanese city pop", "citypop", "city-pop", "tokyo city pop", "80s anime city"],
            "#11132A", "#20294A", "#2D3A5A", "#FF826D", "#69DCE2", "#FFF4E6", "#C0C7DA", "#4F5879"),
        new("solarpunk", "Solarpunk Garden", "Hopeful green technology: living leaf, clean ivory, sunlit gold and clear sky teal.",
            ["solarpunk", "solar punk", "eco futurism", "eco-futurism", "green future"],
            "#F0F7E9", "#D7EAD0", "#B8D7B8", "#2C795D", "#C18A35", "#23382B", "#566F5B", "#B0C9B1"),
        new("coquette", "Coquette", "Soft ballet pink, powder porcelain, rosewood and vintage ribbon tones with a crisp plum anchor.",
            ["coquette", "coquettecore", "balletcore", "ballet core", "soft girl aesthetic", "bow aesthetic"],
            "#FFF5F8", "#F4E1EC", "#E2C9DB", "#B64E7A", "#654272", "#35263A", "#745B77", "#E3C7D8"),
        new("cyber-sigil", "Cyber Sigil", "Internet-era occult futurism: black-violet, acid lime, bruised silver and neon chartreuse.",
            ["cyber sigil", "sigilcore", "cyber sigilism", "internet occult", "dark web aesthetic"],
            "#0B0B12", "#1B1624", "#2B2336", "#C3F53C", "#D35DFF", "#F4F5EF", "#C4C3D0", "#4E425F"),
        new("brooched", "Brooched", "Heirloom jewellery translated into a desktop palette: pearl, deep velvet, antique metal and jewel-tone detail.",
            ["brooched", "brooched trend", "heirloom brooch", "brooch aesthetic", "vintage jewellery aesthetic"],
            "#17121D", "#2A2030", "#3A2B42", "#B89557", "#E8D6B5", "#FAF3E8", "#CBBCCB", "#5A455F"),
        new("scent-stacking", "Scent Stacking", "A layered fragrance palette of smoked amber, vanilla cream, rosewood and polished espresso.",
            ["scent stacking", "scent stacking trend", "perfume layering", "amber vanilla rose", "fragrance aesthetic"],
            "#211711", "#39251D", "#51372A", "#C18445", "#8C4B55", "#FFF1DE", "#D7BCA0", "#765342"),
        new("darecations", "Darecations", "Adrenaline travel with alpine blue, summit snow, warning coral and a punch of expedition orange.",
            ["darecations", "darecation", "darecations trend", "adventure travel aesthetic", "adrenaline rush aesthetic"],
            "#0E1B2A", "#1E3448", "#2B4A60", "#FF6D5A", "#F0C96D", "#F2F7FB", "#B9CAD6", "#456578"),
        new("cabbage-crush", "Cabbage Crush", "Garden-fresh green, pearly pale lilac and deep berry: playful botanical color with a fashion-editor twist.",
            ["cabbage crush", "cabbage crush trend", "vegetable garden aesthetic", "crushed green aesthetic", "botanical maximalism"],
            "#F0F4E5", "#DCE8C8", "#C6D8AE", "#597C42", "#824C70", "#283425", "#5D6F52", "#B6C79C"),
        // Pinterest Palette 2026: distinct art direction for each signature hue, not just a color-word swap.
        new("jade-palette", "Jade Reverie", "Serene jade and moss with porcelain mist: grounded, elegant green that feels botanical but quietly futuristic.",
            ["jade reverie", "jade palette", "pinterest palette jade", "jade green aesthetic", "jade glass", "mint moss", "glamorous green", "jade dreamscape"],
            "#F1F6EF", "#DFEBDD", "#C4D9C6", "#3E7866", "#244F43", "#26352C", "#576C5E", "#B6CFBD"),
        new("plum-noir", "Plum Noir", "Decadent villain-era plum: velvet aubergine, burnt burgundy, smoked brown and one polished, dramatic highlight.",
            ["plum noir", "pinterest palette plum noir", "plum noir palette", "persimmon plum", "villain era", "dark plum luxury", "velvet plum", "burnt burgundy aesthetic"],
            "#140D18", "#241323", "#381C31", "#C58AA4", "#D0A36A", "#F8EEF4", "#D3B5C8", "#58334E"),
        new("wasabi-palette", "Wasabi Voltage", "High-voltage chartreuse against inky charcoal, balanced with acid-lime light and a restrained ultraviolet counterpoint.",
            ["wasabi voltage", "wasabi palette", "pinterest palette wasabi", "wasabi green aesthetic", "electric chartreuse", "acid lime", "high voltage green", "cool wasabi"],
            "#11150A", "#20270F", "#303A17", "#C7F735", "#E2F58E", "#F5F8E9", "#C8D4A9", "#4D5B2B"),
        new("persimmon-palette", "Persimmon Heat", "Persimmon orange-red, sun-warmed porcelain and deep pepper-red: optimistic, tactile and full of movement.",
            ["persimmon heat", "persimmon palette", "pinterest palette persimmon", "persimmon orange", "persimmon red", "sweet heat palette", "persimmon plum"],
            "#FFF3EB", "#F8DDD0", "#EDC0AD", "#C8462F", "#7D2931", "#322522", "#70564F", "#D9A99A"),
        new("sea-witchery", "Sea Witchery", "A dark siren palette of abyssal teal, wet pearl, sea-glass cyan and bruised violet shimmer.",
            ["sea witchery", "sea witchery trend", "mermaid goth", "dark mermaid", "siren aesthetic", "deep sea siren", "ocean witch", "wet pearl aesthetic"],
            "#07191E", "#102B32", "#1B4147", "#55D6C7", "#BE9AE8", "#F0FBF9", "#B3D2D0", "#345D64"),
        new("castlecore", "Castlecore", "Weathered limestone, old parchment, oxblood velvet and antique gold: medieval romance with a grounded stone backbone.",
            ["castlecore", "castle core", "castlecore trend", "medieval aesthetic", "gothic castle", "chainmail aesthetic", "medieval romance", "old castle library"],
            "#17161B", "#28262D", "#39343B", "#A84A5A", "#D2AD67", "#F5F0E6", "#C9C0B2", "#5D5359"),
        new("cherry-coded", "Cherry Coded", "Juicy cherry red and lacquered burgundy on creamy porcelain, with a clean editorial balance rather than candy overload.",
            ["cherry coded", "cherry-coded", "cherry coded trend", "cherry red aesthetic", "cherry cola", "cherry martini aesthetic", "cherry girl", "glossy cherry"],
            "#FFF2ED", "#F3D9D1", "#E7BEB6", "#BE253D", "#662339", "#351F24", "#72525A", "#D8A5A6"),
        new("terra-futura", "Terra Futura", "Hopeful climate-tech: living moss, recycled clay, seedling green and bright sky-teal details.",
            ["terra futura", "terra-futura", "terra futura trend", "future nature", "regenerative future", "earthy futurism", "climate futurism", "eco tech aesthetic"],
            "#F0F5E8", "#DDE8D2", "#C5D7BD", "#367A62", "#B45C3C", "#28372E", "#5A6E60", "#B7C9AC"),
        new("rebel-floats", "Rebel Floats", "Retro soda-shop pop: fizzy cherry, cream-soda vanilla, electric blue and translucent grape, playful without losing contrast.",
            ["rebel floats", "rebel floats trend", "cream soda aesthetic", "soda pop aesthetic", "retro soda shop", "fizzy drink aesthetic", "pickle fix", "chaos cakes"],
            "#FFF4E8", "#F6DFCA", "#EBC5AB", "#C83F55", "#315E9D", "#302329", "#6F5960", "#D9B9A1"),
        new("brat-summer", "Brat Summer", "Deliberately abrasive acid chartreuse, flat black and one toxic-violet interruption: lo-fi, bratty, anti-polish pop energy.",
            ["brat", "brat summer", "brat green", "bratcore", "charli xcx brat", "brat album cover", "brat green aesthetic", "hyperpop brat"],
            "#D4FC39", "#C2E82C", "#AFCF22", "#202019", "#6537B4", "#202018", "#4C5429", "#91AF21"),
        new("office-siren", "Office Siren", "Cold corporate graphite, oxblood lipstick, steel-grey panels and crisp ivory: sharp, nocturnal editorial power dressing.",
            ["office siren", "office siren aesthetic", "corporate siren", "office siren core", "corporate goth", "late night office aesthetic", "editorial office"],
            "#11131A", "#1D2029", "#2C303A", "#C4485C", "#A9B8D0", "#F4F4F6", "#BEC3CD", "#454B59"),
        new("clean-girl", "Clean Girl", "Sunlit minimal polish: warm white, pale oat, soft stone and a fresh sage accent with generous breathing room.",
            ["clean girl", "clean girl aesthetic", "clean aesthetic", "quiet luxury", "minimal luxe", "soft minimalism", "fresh skin aesthetic", "vanilla girl"],
            "#FAF9F4", "#EDEBE2", "#DAD8CE", "#587761", "#425C69", "#2C302C", "#626A61", "#C9C8BD"),
        new("liminal-dream", "Liminal Dream", "Uncanny empty spaces, fluorescent fog and faded aqua with one warning-red signal: nostalgic, surreal and slightly off.",
            ["liminal space", "liminal spaces", "dreamcore", "weirdcore", "backrooms", "empty mall aesthetic", "uncanny nostalgia", "analog horror"],
            "#E8ECE8", "#CDD7D3", "#B7C5C1", "#4D7778", "#A83E45", "#202827", "#566764", "#A9BAB5"),
        new("minecraft-overworld", "Minecraft Overworld", "Block-built adventure with grass green, clear-sky blue, dirt umber and redstone ember accents.",
            ["minecraft", "minecraft overworld", "minecraft survival", "minecraft redstone", "minecraft build", "block game aesthetic", "voxel game", "grass block aesthetic"],
            "#EAF4E7", "#D7E8D1", "#BDD5BA", "#3E8552", "#9D552D", "#25372B", "#596F5B", "#A9C8A6"),
        new("sailor-moon", "Moon Prism", "Magical-girl starlight: moonlit indigo, pearl, rose-pink and polished crescent-gold with a dreamy celestial finish.",
            ["sailor moon", "sailormoon", "moon prism", "moon prism power", "magical girl anime", "lunar princess", "sailor senshi"],
            "#11132B", "#202448", "#30345D", "#F47BA8", "#F1D27A", "#F8F6FF", "#C4C6E4", "#4C507E"),
        new("dune-spice", "Arrakis Spice", "Burnished desert sand, spice amber, mineral umber and night-sky indigo: austere science fiction with heat-haze depth.",
            ["dune", "dune part two", "arrakis", "spice melange", "fremen", "desert sci fi", "desert planet aesthetic"],
            "#1A1718", "#302722", "#48372A", "#D28A42", "#9DADB9", "#F5EBDD", "#CDBCA8", "#695443"),
        new("wicked-emerald", "Emerald Stage", "A theatrical emerald-and-rose palette: deep green velvet, fuchsia stage light, warm pearl and dramatic spotlight contrast.",
            ["wicked", "wicked musical", "wicked movie", "elphaba", "glinda", "emerald city musical", "emerald and pink"],
            "#101B17", "#1A2D24", "#294033", "#5DCC8C", "#F18BBE", "#F3F8F3", "#BBD2C1", "#476D59"),
        new("kpop-demon-hunters", "Idol Exorcist", "A supernatural concert-stage palette of midnight plum, electric violet, hot pink and a controlled solar-gold flash.",
            ["kpop demon hunters", "k-pop demon hunters", "demon hunters soundtrack", "huntrix", "saja boys", "supernatural kpop", "idol exorcist"],
            "#100A1A", "#23112F", "#351B45", "#F44CCB", "#6D8DFF", "#FFF1FD", "#CAB6DE", "#583264"),
        // Pinterest's 2026 festival trend report: palettes authored as complete art directions.
        new("hot-tropic", "Hot Tropic", "Tropical showgirl maximalism: carnival fuchsia, guava coral and sun-struck gold over a deep after-dark plum.",
            ["hot tropic", "hot tropic trend", "tropicoqueta", "tropical showgirl", "showgirl aesthetic", "gold fringe dress", "cumbia inspired makeup", "desert carnival", "body glitter", "carnaval glamour"],
            "#160D1E", "#28162C", "#3A2040", "#FF6B83", "#FFD166", "#FFF4F1", "#D7B9CB", "#5D3A4F"),
        new("swashbuckled", "Swashbuckled", "A rogue-adventurer palette of weathered suede, tarnished brass, storm-sea teal and worn leather under moonlit charcoal.",
            ["swashbuckled", "swashbuckled trend", "piratecore", "pirate eye makeup", "pirate earrings", "pirate belt", "belts on belts", "suede mini skirt", "nautical renegade", "booted adventurer"],
            "#171512", "#28221B", "#3D3226", "#C68D54", "#6D9F9B", "#F4EBDC", "#CDBDA4", "#594938"),
        new("shoegaze", "Shoegaze Aesthetic", "Worn-in indie melancholy: smoke-black denim, faded rose, washed cobalt and flannel blue with a lived-in 2000s edge.",
            ["shoegaze", "shoegaze aesthetic", "shoegaze aesthetic outfits", "2000s rock outfits", "indie alternative outfits", "thrifted band tees", "worn in flannel", "deep cut indie", "windbreaker jacket outfit", "garage band aesthetic"],
            "#12151D", "#232630", "#333642", "#D7839B", "#79ABC7", "#F3F0F3", "#B7BBC8", "#454B57"),
        new("peak-poptimism", "Peak Poptimism", "Y2K pop maximalism with airbrushed candy gradients, holographic lilac, rhinestone magenta and laser-cyan shine.",
            ["peak poptimism", "peak poptimism trend", "airbrush jeans", "rhinestone tops", "frutiger aero core", "belt charms", "uv resin jewelry", "pop maximalism", "glitter is the new neutral", "holographic pop"],
            "#141026", "#251A3B", "#3B2854", "#FF64C8", "#59E8FF", "#F8F1FF", "#C9B9DE", "#594371"),
        new("sheer-euphoria", "Sheer Euphoria", "Soft-focus rave euphoria: sheer lilac, organza blush and silk-mint light against a deep dancefloor violet.",
            ["sheer euphoria", "sheer euphoria trend", "underground rave outfit", "sheer organza", "sheer skirt outfit", "organza shirt outfit", "angelic accessories", "oversized silk shirt", "soft rave", "euphoric dancefloor"],
            "#171222", "#282035", "#3C2E4B", "#E9A9D2", "#9FE1D6", "#F8F2FC", "#D0C3DB", "#574768"),
        new("high-camp", "High Camp", "Theatrical chaos with fearless coral, absurdist turquoise, velvet plum and spotlight ivory; too much is the point.",
            ["high camp", "high camp trend", "campy aesthetic", "theater kid aesthetic", "mismatched outfit", "dramatic eye makeup", "outfit sandwich", "costume without occasion", "whimsical chaos", "maximalist performance"],
            "#141118", "#29202B", "#423342", "#FF6B5A", "#68F0D2", "#FFF4F0", "#D8C1D7", "#684C69"),
    ];

    public static IReadOnlyList<Profile> Profiles => AllProfiles;

    public static Profile? Find(string? prompt)
        => FindAll(prompt).FirstOrDefault();

    /// <summary>
    /// Returns distinct recognized profiles ordered by phrase specificity and then first position
    /// in the prompt. Negated references ("not cyberpunk", "without vaporwave") do not count as
    /// positive aesthetic intent.
    /// </summary>
    public static IReadOnlyList<Profile> FindAll(string? prompt)
    {
        var normalized = Normalize(prompt);
        if (normalized.Length == 0) return Array.Empty<Profile>();

        var padded = $" {normalized} ";
        var matches = new List<(Profile Profile, int Score, int Position)>();
        foreach (var profile in AllProfiles)
        {
            int bestScore = -1;
            int bestPosition = int.MaxValue;
            foreach (var alias in profile.Aliases.Prepend(profile.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length == 0) continue;

                var paddedAlias = $" {normalizedAlias} ";
                int searchFrom = 0;
                while (searchFrom < padded.Length)
                {
                    int index = padded.IndexOf(paddedAlias, searchFrom, StringComparison.Ordinal);
                    if (index < 0) break;
                    int aliasStart = index + 1;
                    var prefix = padded[..aliasStart].TrimEnd();
                    // Normalize exact and fuzzy match positions to zero-based word indices. The
                    // prior exact matcher stored a character offset while fuzzy matches stored a
                    // word index, which could reorder blends whenever one reference was misspelled.
                    int tokenPosition = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    searchFrom = index + paddedAlias.Length;

                    if (IsNegatedReference(prefix)) continue;

                    // Prefer the most specific alias for a single aesthetic: a franchise/sub-style
                    // phrase wins over an incidental mention of a broad parent term.
                    int tokenCount = normalizedAlias.Count(ch => ch == ' ') + 1;
                    int score = tokenCount * 1000 + normalizedAlias.Length;
                    if (normalized.Equals(normalizedAlias, StringComparison.Ordinal)) score += 10000;

                    if (score > bestScore || (score == bestScore && tokenPosition < bestPosition))
                    {
                        bestScore = score;
                        bestPosition = tokenPosition;
                    }
                }
            }

            if (bestScore >= 0)
                matches.Add((profile, bestScore, bestPosition));
        }

        // If no exact named aesthetic was found (or the user explicitly asks for a
        // blend but one reference is misspelled), run a conservative phrase-level fuzzy pass.
        // This is deliberately separate from ColorLexicon fuzzy matching: aesthetic names are
        // multi-token concepts and should not collapse into a random nearest colour word.
        // These two 2026 Pinterest Palette pairings are compound aesthetic names, not a
        // free-form connector elsewhere in the prompt. Keeping them separate prevents an unrelated
        // "hard edges and spacious layout" modifier from collapsing both source profiles together.
        bool isNamedPaletteBlend =
            normalized.Contains("cool wasabi", StringComparison.Ordinal) ||
            normalized.Contains("persimmon plum", StringComparison.Ordinal);
        bool hasBlendConnector = HasBlendConnector(normalized) && !isNamedPaletteBlend;
        bool hasSpecificExactMatch = matches.Any(match => match.Score >= 2000);
        if (matches.Count == 0 || hasBlendConnector || !hasSpecificExactMatch)
        {
            var fuzzyMatches = FindFuzzyProfiles(normalized, allowMultiple: hasBlendConnector);
            if (hasBlendConnector)
            {
                // Explicit blends may contain incidental one-word aliases alongside a misspelled
                // full reference. Merge both paths, then keep the strongest match at each token
                // position before taking the first two references in the prompt.
                foreach (var match in fuzzyMatches)
                {
                    int existingIndex = matches.FindIndex(existing =>
                        existing.Profile.Id.Equals(match.Profile.Id, StringComparison.OrdinalIgnoreCase) &&
                        existing.Position == match.Position);
                    if (existingIndex < 0)
                    {
                        matches.Add(match);
                    }
                    else if (match.Score > matches[existingIndex].Score)
                    {
                        // Replace a broad exact alias ("neon") with the stronger fuzzy full-name
                        // match ("Neon Afterglw") at the same word position.
                        matches[existingIndex] = match;
                    }
                }
            }
            else if (fuzzyMatches.Count > 0 &&
                     (matches.Count == 0 || fuzzyMatches[0].Score > matches.Max(match => match.Score)))
            {
                // "Neon Afterglw" can still match the broad alias "neon" exactly. Prefer the
                // stronger multiword fuzzy match so the named aesthetic isn't lost.
                matches.Clear();
                matches.Add(fuzzyMatches[0]);
            }
        }

        var orderedMatches = hasBlendConnector
            ? matches.GroupBy(match => match.Position)
                .Select(group => group.OrderByDescending(match => match.Score).First())
                .OrderBy(match => match.Position)
            : matches.OrderByDescending(match => match.Score).ThenBy(match => match.Position);

        var orderedProfiles = new List<Profile>();
        foreach (var match in orderedMatches)
        {
            if (orderedProfiles.Any(profile =>
                profile.Id.Equals(match.Profile.Id, StringComparison.OrdinalIgnoreCase)))
                continue;
            orderedProfiles.Add(match.Profile);
            if (hasBlendConnector && orderedProfiles.Count == 2)
                break;
        }

        // The published 2026 Palette pair is named in color order; duplicate phrase aliases can
        // tie on score/position, so preserve that canonical order for the blended theme name.
        var normalizedPrompt = Normalize(prompt);
        if (normalizedPrompt.Contains("cool wasabi", StringComparison.Ordinal))
        {
            orderedProfiles = orderedProfiles
                .OrderBy(profile => profile.Id == "cool-blue" ? 0 : profile.Id == "wasabi-palette" ? 1 : 2)
                .ToList();
        }
        else if (normalizedPrompt.Contains("persimmon plum", StringComparison.Ordinal))
        {
            orderedProfiles = orderedProfiles
                .OrderBy(profile => profile.Id == "persimmon-palette" ? 0 : profile.Id == "plum-noir" ? 1 : 2)
                .ToList();
        }

        return orderedProfiles;
    }

    private static List<(Profile Profile, int Score, int Position)> FindFuzzyProfiles(string normalized, bool allowMultiple = false)
    {
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = new List<(Profile Profile, int Score, int Position, int Edits)>();

        foreach (var profile in AllProfiles)
        {
            int bestScore = int.MinValue;
            int bestPosition = int.MaxValue;
            int bestEdits = int.MaxValue;

            foreach (var alias in profile.Aliases.Prepend(profile.Name))
            {
                var aliasWords = Normalize(alias).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (aliasWords.Length == 0 || aliasWords.Length > words.Length) continue;

                for (int start = 0; start <= words.Length - aliasWords.Length; start++)
                {
                    int edits = 0;
                    bool valid = true;
                    for (int i = 0; i < aliasWords.Length; i++)
                    {
                        if (string.Equals(words[start + i], aliasWords[i], StringComparison.Ordinal))
                            continue;

                        int maxDistance = aliasWords[i].Length >= 9 ? 2 : aliasWords[i].Length >= 4 ? 1 : 0;
                        if (maxDistance == 0 || Math.Abs(words[start + i].Length - aliasWords[i].Length) > maxDistance)
                        {
                            valid = false;
                            break;
                        }

                        int distance = EditDistanceAtMost(words[start + i], aliasWords[i], maxDistance);
                        if (distance > maxDistance)
                        {
                            valid = false;
                            break;
                        }
                        edits += distance;
                        // A short phrase with several typos is too ambiguous to be a reliable reference.
                        if (edits > 2)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid || edits == 0) continue;
                    var prefix = string.Join(' ', words.Take(start));
                    if (IsNegatedReference(prefix)) continue;

                    int score = aliasWords.Length * 1000 + Normalize(alias).Length - edits * 450;
                    // Use zero-based word indices here, matching the exact-alias matcher above.
                    if (score > bestScore || (score == bestScore && start < bestPosition))
                    {
                        bestScore = score;
                        bestPosition = start;
                        bestEdits = edits;
                    }
                }
            }

            if (bestScore > int.MinValue)
                matches.Add((profile, bestScore, bestPosition, bestEdits));
        }

        // Never guess between several near-ties; a fuzzy result is used only when the best match
        // is clearly stronger than the next alternative. Exact matches always win upstream.
        var ordered = matches
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Edits)
            .ThenBy(m => m.Position)
            .ToList();
        if (ordered.Count == 0) return new List<(Profile, int, int)>();

        // A single-reference prompt must have a clear best match. For an explicit blend, however,
        // equally plausible results often represent the two requested references (e.g. two names
        // with one typo each), so return ranked matches and let FindAll collect the first two
        // distinct profiles in prompt order.
        if (!allowMultiple && ordered.Count > 1 && ordered[0].Score - ordered[1].Score < 180)
            return new List<(Profile, int, int)>();
        return ordered.Select(m => (m.Profile, m.Score, m.Position)).ToList();
    }

    private static int EditDistanceAtMost(string source, string target, int limit)
    {
        if (Math.Abs(source.Length - target.Length) > limit) return limit + 1;
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (int j = 0; j <= target.Length; j++) previous[j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            int rowMin = current[0];
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (rowMin > limit) return limit + 1;
            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static bool HasBlendConnector(string normalized)
    {
        var text = $" {normalized} ";
        string[] connectors =
        [
            " meets ", " x ", " + ", " blend ", " blended with ", " mix ", " mixed with ",
            " mashup ", " mash up ", " fusion ", " fused with ", " and "
        ];
        return connectors.Any(connector => text.Contains(connector, StringComparison.Ordinal));
    }

    /// <summary>
    /// A prompt that explicitly joins two aesthetic references asks for a blend rather than a
    /// single nearest match. Ordinary descriptive prompts can mention incidental styles without
    /// unintentionally turning into a mashup.
    /// </summary>
    public static bool RequestsBlend(string? prompt, IReadOnlyList<Profile> matches)
    {
        if (matches.Count < 2) return false;

        var text = Normalize(prompt);
        // Pinterest Palette publishes these pairings as named sub-palettes. They are an explicit
        // composition request even when the user types the name without "mix" or "and".
        if (text.Contains("cool wasabi", StringComparison.Ordinal) ||
            text.Contains("persimmon plum", StringComparison.Ordinal))
            return true;

        return HasBlendConnector(text);
    }

    private static bool IsNegatedReference(string prefix)
    {
        var words = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;

        var last = words[^1];
        if (last is "not" or "no" or "never" or "without" or "avoid" or "skip" or "exclude" or "less")
            return true;

        // Natural prompts often insert a short bridge word: "without the neon", "not too glossy",
        // "avoid using chrome". Match only the immediate phrase before the cue so a later positive
        // clause (for example "avoid neon but use cyan") is not accidentally negated too.
        if (words.Length >= 2)
        {
            var pair = $"{words[^2]} {words[^1]}";
            if (pair is "dont use" or "dont want" or "dont like" or "do not" or
                "not like" or "not really" or "not too" or "not very" or
                "without the" or "without using" or "without being" or
                "avoid using" or "avoid the" or "skip using" or "skip the" or
                "rather than" or "instead of")
                return true;
        }

        if (words.Length >= 2)
        {
            var pair = $"{words[^2]} {words[^1]}";
            if (pair is "dont use" or "dont want" or "dont like" or "do not" or "not like" or "not really" or "rather than" or "instead of")
                return true;
        }

        if (words.Length >= 3)
        {
            var triple = $"{words[^3]} {words[^2]} {words[^1]}";
            if (triple is "do not use" or "dont want the" or "not a fan")
                return true;
        }

        return false;
    }

    public static CozyTheme CreateTheme(Profile profile, string prompt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var theme = CreateProfileBaseTheme(profile);
        ApplyPromptModifiers(theme, prompt);
        PaletteQualityValidator.ValidateAndRepair(theme);
        return theme;
    }

    private static CozyTheme CreateProfileBaseTheme(Profile profile)
        => new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = profile.Name,
            Description = profile.Description,
            IsBuiltIn = false,
            LastModified = DateTimeOffset.UtcNow,
            BackgroundBase = profile.BackgroundBase,
            BackgroundAlt = profile.BackgroundAlt,
            Surface = profile.Surface,
            AccentPrimary = profile.AccentPrimary,
            AccentStrong = profile.AccentStrong,
            TextPrimary = profile.TextPrimary,
            TextMuted = profile.TextMuted,
            BorderSubtle = profile.BorderSubtle,
        };

    /// <summary>
    /// Blends two curated references by role: the primary profile owns the background identity,
    /// while the secondary profile lends more of its surface and accent character. OKLCH blending
    /// follows the shortest hue arc and the result is quality-checked after composition.
    /// </summary>
    public static CozyTheme CreateBlendedTheme(Profile primary, Profile secondary, string prompt)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);

        // Blend the unmodified reference palettes, then apply the prompt's art direction once
        // to the composed result. Applying modifiers to both inputs before blending diluted hue
        // intent, duplicated description metadata, and sometimes softened hard-edge requests.
        var first = CreateProfileBaseTheme(primary);
        var second = CreateProfileBaseTheme(secondary);
        bool usesFrutigerGeometry =
            primary.Id.Equals("frutiger-aero", StringComparison.OrdinalIgnoreCase) ||
            secondary.Id.Equals("frutiger-aero", StringComparison.OrdinalIgnoreCase);
        var blended = new CozyTheme
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{primary.Name} × {secondary.Name}",
            Description = $"{primary.Description} Blended with {secondary.Description}",
            IsBuiltIn = false,
            LastModified = DateTimeOffset.UtcNow,
            BackgroundBase = BlendOklch(first.BackgroundBase, second.BackgroundBase, 0.15f),
            BackgroundAlt = BlendOklch(first.BackgroundAlt, second.BackgroundAlt, 0.23f),
            Surface = BlendOklch(first.Surface, second.Surface, 0.34f),
            AccentPrimary = BlendOklch(first.AccentPrimary, second.AccentPrimary, 0.45f),
            AccentStrong = BlendOklch(first.AccentStrong, second.AccentStrong, 0.58f),
            TextPrimary = BlendOklch(first.TextPrimary, second.TextPrimary, 0.12f),
            TextMuted = BlendOklch(first.TextMuted, second.TextMuted, 0.20f),
            BorderSubtle = BlendOklch(first.BorderSubtle, second.BorderSubtle, 0.25f),
            CornerRadiusScale = usesFrutigerGeometry ? 1.25 : 1.0,
            DensityScale = 1.0
        };

        ApplyPromptModifiers(blended, prompt);
        PaletteQualityValidator.ValidateAndRepair(blended);
        return blended;
    }

    private static string BlendOklch(string first, string second, float amount)
    {
        var (l1, c1, h1) = ColorMath.HexToOklch(first);
        var (l2, c2, h2) = ColorMath.HexToOklch(second);
        float hueDelta = h2 - h1;
        if (hueDelta > 180f) hueDelta -= 360f;
        if (hueDelta < -180f) hueDelta += 360f;

        return ColorMath.OklchToHex(
            l1 + (l2 - l1) * amount,
            c1 + (c2 - c1) * amount,
            h1 + hueDelta * amount);
    }

    /// <summary>
    /// Applies compositional and visual-language modifiers to a palette generated from free-form
    /// color NLP, not only palettes anchored to a named aesthetic profile.
    /// </summary>
    public static void ApplyPromptModifiers(CozyTheme theme, string prompt)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var text = Normalize(prompt);

        // Use OKLCH for all prompt-relative edits so the same "lighter", "muted" or hue shift
        // behaves more predictably across blue, yellow, magenta and green palettes.
        var baseColor = ColorMath.HexToOklch(theme.BackgroundBase);
        var altColor = ColorMath.HexToOklch(theme.BackgroundAlt);
        var surfaceColor = ColorMath.HexToOklch(theme.Surface);
        var primaryColor = ColorMath.HexToOklch(theme.AccentPrimary);
        var strongColor = ColorMath.HexToOklch(theme.AccentStrong);
        var borderColor = ColorMath.HexToOklch(theme.BorderSubtle);

        var lightnessDelta = 0f;
        if (HasAny(text, "darker", "make it darker", "darken", "darken the palette", "less bright", "dim", "dim it", "dimmer", "make it moodier"))
            lightnessDelta = -0.045f;
        else if (HasAny(text, "lighter", "make it lighter", "lighten", "lighten the palette", "brighter", "make it brighter", "brighten it", "brighter background", "airy", "sunlit"))
            lightnessDelta = 0.045f;
        else if (HasAny(text, "cinematic", "noir", "moody", "after dark", "late night"))
            lightnessDelta = -0.025f;

        // OKLCH chroma is not HSL saturation; numeric changes preserve a reference's hue family
        // while independently responding to "pastel", "matte", "glossy", "dopamine" and "electric".
        var chromaDelta = 0f;
        if (HasAny(text, "more muted", "muted", "desaturated", "less saturated", "soften the colors", "softer", "subtle", "faded", "matte", "pastel", "quiet luxury"))
            chromaDelta = -0.025f;
        else if (HasAny(text, "more vibrant", "vibrant", "vivid", "more saturated", "saturated", "punchier", "electric", "extra neon", "neon", "dopamine", "colorful", "bold", "bolder", "maximalist", "camp", "brat", "hyperpop"))
            chromaDelta = 0.025f;

        // Named color cues use conventional HSL hue degrees (the same intuitive scale exposed
        // by the UI color picker). Do not compare those values directly with OKLCH hue: its
        // perceptual hue angle differs materially for saturated violet/cyan/green colors.
        var hueTarget = MatchHueModifier(text);
        var hueShift = 0f;
        if (hueTarget is null && HasAny(text, "warm", "warmer", "more warm", "warmer tones", "cozy", "cozier", "warmth", "amber glow"))
            hueShift = 10;
        else if (hueTarget is null && HasAny(text, "cool", "cooler", "more cool", "cold", "colder", "cooler tones", "make it icy", "frosted", "glacier"))
            hueShift = -10;

        theme.BackgroundBase = ColorMath.OklchToHex(
            baseColor.L + lightnessDelta * 0.7f, baseColor.C + chromaDelta * 0.20f, baseColor.H + hueShift);
        theme.BackgroundAlt = ColorMath.OklchToHex(
            altColor.L + lightnessDelta * 0.7f, altColor.C + chromaDelta * 0.25f, altColor.H + hueShift);
        theme.Surface = ColorMath.OklchToHex(
            surfaceColor.L + lightnessDelta * 0.7f, surfaceColor.C + chromaDelta * 0.35f, surfaceColor.H + hueShift);
        theme.AccentPrimary = ColorMath.OklchToHex(
            primaryColor.L + lightnessDelta, primaryColor.C + chromaDelta, primaryColor.H + hueShift);
        theme.AccentStrong = ColorMath.OklchToHex(
            strongColor.L + lightnessDelta, strongColor.C + chromaDelta, strongColor.H + hueShift);
        theme.BorderSubtle = ColorMath.OklchToHex(
            borderColor.L + lightnessDelta * 0.5f, borderColor.C + chromaDelta * 0.15f, borderColor.H + hueShift);

        if (hueTarget is { } requestedHue)
        {
            var currentBackgroundHue = ColorMath.HexToHsl(theme.BackgroundBase).H;
            var hueDelta = ShortestHueDelta(currentBackgroundHue, requestedHue);

            // Preserve each role's saturation/lightness and hue relationships while shifting the
            // entire palette in the color-picker's familiar HSL hue space.
            theme.BackgroundBase = ShiftHslHue(theme.BackgroundBase, hueDelta);
            theme.BackgroundAlt = ShiftHslHue(theme.BackgroundAlt, hueDelta);
            theme.Surface = ShiftHslHue(theme.Surface, hueDelta);
            theme.AccentPrimary = ShiftHslHue(theme.AccentPrimary, hueDelta);
            theme.AccentStrong = ShiftHslHue(theme.AccentStrong, hueDelta);
            theme.BorderSubtle = ShiftHslHue(theme.BorderSubtle, hueDelta);
        }

        // Treat accent requests as accents, not as permission to repaint the whole background.
        // This keeps a named reference recognizable (e.g. "Frutiger Aero with violet accents").
        var accentHueTarget = MatchAccentHueModifier(text);
        if (accentHueTarget is { } requestedAccentHue)
        {
            var currentPrimaryHue = ColorMath.HexToHsl(theme.AccentPrimary).H;
            var accentDelta = ShortestHueDelta(currentPrimaryHue, requestedAccentHue);

            // "Violet accents" means violet in the app's familiar HSL color-picker scale, not
            // OKLCH's differently oriented hue angle. Shift both accent roles together so the
            // pair remains harmonious while leaving background identity untouched.
            theme.AccentPrimary = SetHslHue(theme.AccentPrimary, requestedAccentHue);
            theme.AccentStrong = ShiftHslHue(theme.AccentStrong, accentDelta);
        }

        var artDirection = new List<string>();
        if (HasAny(text, "glass", "glassy", "glassmorphism", "liquid glass", "frutiger aero", "holographic", "glossy", "chrome"))
        {
            theme.CornerRadiusScale = Math.Max(theme.CornerRadiusScale, 1.22);
            // Glass-like surfaces read better with breathing room; DensityScale scales spacing
            // tokens upward, so values below 1.0 would make these layouts cramped.
            theme.DensityScale = Math.Clamp(theme.DensityScale * 1.04, 0.5, 2.0);
            theme.Surface = SetOklchLightness(theme.Surface, Math.Min(0.92f, ColorMath.HexToOklch(theme.Surface).L + 0.025f));
            artDirection.Add("glossy, glass-like surfaces");
        }
        if (HasAny(text, "brutalist", "brutalism", "hard edges", "industrial", "raw concrete", "terminal ui"))
        {
            theme.CornerRadiusScale = 0.55;
            artDirection.Add("hard-edged geometry");
        }
        if (HasAny(text, "minimal", "minimalist", "clean girl", "quiet luxury", "less clutter", "focus desk"))
        {
            // DensityScale scales spacing tokens, so a minimal composition needs more
            // whitespace rather than the tighter spacing created by a value below 1.0.
            theme.DensityScale = Math.Clamp(theme.DensityScale * 1.10, 0.5, 2.0);
            artDirection.Add("restrained, spacious composition");
        }
        if (HasAny(text, "spacious", "airy layout", "breathing room", "lots of whitespace"))
        {
            theme.DensityScale = Math.Clamp(theme.DensityScale * 1.12, 0.5, 2.0);
            artDirection.Add("generous negative space");
        }
        if (HasAny(text, "maximalist", "dopamine", "eclectic", "camp", "playful maximalism"))
        {
            theme.CornerRadiusScale = Math.Clamp(Math.Max(theme.CornerRadiusScale, 1.08), 0.25, 2.0);
            artDirection.Add("expressive maximalist accents");
        }

        if (artDirection.Count > 0)
            theme.Description = $"{theme.Description} Art direction: {string.Join(", ", artDirection.Distinct(StringComparer.OrdinalIgnoreCase))}."; 
    }

    private static float ShortestHueDelta(float fromHue, float toHue)
    {
        var delta = toHue - fromHue;
        if (delta > 180f) delta -= 360f;
        if (delta < -180f) delta += 360f;
        return delta;
    }

    private static string ShiftHslHue(string hex, float delta)
    {
        var (hue, saturation, lightness) = ColorMath.HexToHsl(hex);
        return ColorMath.HslToHex(hue + delta, saturation, lightness);
    }

    private static string SetHslHue(string hex, float hue)
    {
        var (_, saturation, lightness) = ColorMath.HexToHsl(hex);
        return ColorMath.HslToHex(hue, saturation, lightness);
    }

    private static string SetOklchLightness(string hex, float lightness)
    {
        var (_, chroma, hue) = ColorMath.HexToOklch(hex);
        return ColorMath.OklchToHex(lightness, chroma, hue);
    }

    private static float? MatchAccentHueModifier(string text)
    {
        // Accent-scoped language modifies the two accent roles only. Support both natural
        // adjective order ("violet accents") and imperative phrasing ("make the accents violet").
        var cues = new (string[] Phrases, float Hue)[]
        {
            (["red accents", "red highlights", "coral accents", "cherry accents", "scarlet details", "make accents red", "make the accents red", "more red accents", "crimson accents"], 0),
            (["orange accents", "amber accents", "persimmon accents", "make accents orange", "more orange accents"], 28),
            (["gold accents", "brass accents", "gold details", "make accents gold", "more gold accents"], 45),
            (["yellow accents", "wasabi accents", "chartreuse accents", "make accents yellow", "more yellow accents"], 65),
            (["green accents", "jade accents", "emerald accents", "sage accents", "make accents green", "more green accents"], 135),
            (["teal accents", "sea-glass accents", "make accents teal", "more teal accents"], 180),
            (["cyan accents", "cyan highlights", "aqua accents", "make accents cyan", "more cyan accents"], 190),
            (["blue accents", "blue highlights", "cobalt accents", "make accents blue", "more blue accents"], 220),
            (["indigo accents", "indigo highlights", "make accents indigo", "more indigo accents"], 245),
            (["purple accents", "violet accents", "ultraviolet accents", "make accents purple", "make accents violet", "make the accents violet", "more violet accents", "more purple accents"], 275),
            (["magenta accents", "fuchsia accents", "make accents magenta", "more magenta accents"], 315),
            (["pink accents", "rose accents", "blush accents", "make accents pink", "more pink accents"], 335),
        };

        foreach (var (phrases, hue) in cues)
            if (HasAny(text, phrases)) return hue;
        return null;
    }

    private static float? MatchHueModifier(string text)
    {
        // Palette-scoped phrases are intentionally distinct from accent-only cues. If the user
        // asks for violet accents, don't rotate the entire background into violet as well.
        var cues = new (string[] Phrases, float Hue)[]
        {
            (["more red", "make it red", "in red", "make everything red", "shift the palette toward red", "lean red", "more crimson overall"], 0),
            (["more orange", "make it orange", "in orange", "shift the palette toward orange", "lean orange"], 28),
            (["more gold", "make it gold", "in gold", "shift the palette toward gold"], 45),
            (["more yellow", "make it yellow", "in yellow", "shift the palette toward yellow"], 55),
            (["more green", "make it green", "in green", "shift the palette toward green", "lean green"], 125),
            (["more teal", "make it teal", "in teal", "shift the palette toward teal"], 180),
            (["more cyan", "make it cyan", "in cyan", "shift the palette toward cyan"], 190),
            (["more blue", "make it blue", "in blue", "bluer", "shift the palette toward blue", "lean blue"], 220),
            (["more indigo", "make it indigo", "in indigo", "shift the palette toward indigo"], 245),
            (["more purple", "make it purple", "in purple", "more violet", "make it violet", "in violet", "shift the palette toward violet", "shift the palette toward purple"], 275),
            (["more magenta", "make it magenta", "in magenta", "shift the palette toward magenta"], 315),
            (["more pink", "make it pink", "in pink", "pinker", "shift the palette toward pink"], 335),
        };

        foreach (var (phrases, hue) in cues)
            if (HasAny(text, phrases)) return hue;
        return null;
    }

    private static bool HasAny(string text, params string[] phrases)
        => phrases.Any(phrase => ContainsPositivePhrase(text, Normalize(phrase)));

    /// <summary>
    /// Checks a whole normalized phrase, ignoring occurrences negated immediately before the cue.
    /// Shared by aesthetic modifiers and ranking so "not neon" cannot receive a neon/vibrancy
    /// bonus and accidental substrings in unrelated words don't trigger art direction.
    /// </summary>
    public static bool ContainsPositiveCue(string? prompt, string phrase)
        => ContainsPositivePhrase(Normalize(prompt), Normalize(phrase));

    private static bool ContainsPositivePhrase(string normalizedText, string normalizedPhrase)
    {
        if (normalizedText.Length == 0 || normalizedPhrase.Length == 0) return false;
        var paddedText = $" {normalizedText} ";
        var paddedPhrase = $" {normalizedPhrase} ";
        int searchFrom = 0;
        while (searchFrom < paddedText.Length)
        {
            int index = paddedText.IndexOf(paddedPhrase, searchFrom, StringComparison.Ordinal);
            if (index < 0) return false;
            var prefix = paddedText[..index].TrimEnd();
            if (!IsNegatedReference(prefix)) return true;
            searchFrom = index + 1;
        }
        return false;
    }

    /// <summary>Exposes the catalog's shared phrase normalization to ranking and diagnostics.</summary>
    public static string NormalizeForMatching(string? value) => Normalize(value);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lower = value.ToLowerInvariant().Replace("’", "'");
        lower = lower.Replace("don't", "dont", StringComparison.Ordinal)
            .Replace("doesn't", "doesnt", StringComparison.Ordinal)
            .Replace("can't", "cant", StringComparison.Ordinal)
            .Replace("isn't", "isnt", StringComparison.Ordinal)
            .Replace("won't", "wont", StringComparison.Ordinal);
        lower = Regex.Replace(lower, @"[^a-z0-9]+", " ");
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }
}
