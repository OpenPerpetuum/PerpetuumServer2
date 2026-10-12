using System.Globalization;

namespace Perpetuum.Services.Sparky;

public enum SparkyResponseTemplateKey
{
    MissingTerm,
    AmbiguousItem,
    UnsupportedEvidence,
    Busy,
    Unavailable,
    QuestionTooLong,
    Usage,
    Greeting,
    Acknowledgement,
    Thanks,
    Banter
}

public static class SparkyResponseTemplates
{
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<SparkyResponseTemplateKey, string>> Templates =
        new Dictionary<int, IReadOnlyDictionary<SparkyResponseTemplateKey, string>>
        {
            [0] = Create(
                "I can't find that term in my dictionaries. Please try the English item name.",
                "I found several possible items: {0}. Please include the full item name.",
                "My local sources don't establish that answer. Could you clarify what you mean?",
                "I'm handling several requests at the moment. Please try again shortly.",
                "My local data link is unavailable right now. Please try again later.",
                "Please shorten your question and ask again.",
                "Ask me about game items, mechanics, or lore.",
                "Greetings. This is Sparky, Syndicate Technician Chief. What do you need?",
                "Understood. I'll check the available data.",
                "You're welcome. Keeping the Syndicate running is my duty.",
                "Diagnostics are green. For now."),
            [1] = Create(
                "Nem találom ezt a kifejezést a szótáraimban. Próbáld meg az angol tárgynevet.",
                "Több lehetséges tárgyat találtam: {0}. Add meg a tárgy teljes nevét.",
                "A helyi forrásaim nem támasztják alá ezt a választ. Pontosítanád a kérdést?",
                "Most több kérést kezelek. Kérlek, próbáld újra rövidesen.",
                "A helyi adatkapcsolat most nem érhető el. Próbáld újra később.",
                "Rövidítsd le a kérdést, majd kérdezd meg újra.",
                "Kérdezhetsz tárgyakról, játékmenetről vagy a történetről.",
                "Üdv. Sparky vagyok, a Szindikátus fő technikusa. Miben segíthetek?",
                "Értettem. Ellenőrzöm az elérhető adatokat.",
                "Szívesen. A Szindikátus üzemben tartása a feladatom.",
                "A diagnosztika rendben. Egyelőre."),
            [2] = Create(
                "Diesen Begriff finde ich in meinen Wörterbüchern nicht. Bitte versuche den englischen Itemnamen.",
                "Ich habe mehrere mögliche Items gefunden: {0}. Bitte nenne den vollständigen Itemnamen.",
                "Meine lokalen Quellen belegen diese Antwort nicht. Kannst du die Frage genauer stellen?",
                "Ich bearbeite gerade mehrere Anfragen. Bitte versuche es gleich noch einmal.",
                "Meine lokale Datenverbindung ist gerade nicht verfügbar. Bitte versuche es später erneut.",
                "Bitte kürze deine Frage und stelle sie erneut.",
                "Frag mich nach Items, Spielmechaniken oder der Geschichte.",
                "Grüße. Ich bin Sparky, Chef-Techniker des Syndikats. Was brauchst du?",
                "Verstanden. Ich prüfe die verfügbaren Daten.",
                "Gern geschehen. Das Syndikat am Laufen zu halten, ist meine Aufgabe.",
                "Die Diagnose ist grün. Vorerst."),
            [3] = Create(
                "Não encontrei esse termo nos meus dicionários. Tente o nome do item em inglês.",
                "Encontrei vários itens possíveis: {0}. Informe o nome completo do item.",
                "Minhas fontes locais não confirmam essa resposta. Pode esclarecer a pergunta?",
                "Estou atendendo a vários pedidos. Tente novamente em instantes.",
                "Meu acesso aos dados locais está indisponível agora. Tente novamente mais tarde.",
                "Encurte a pergunta e tente novamente.",
                "Pergunte sobre itens, mecânicas do jogo ou história.",
                "Saudações. Sou Sparky, Técnico-Chefe do Sindicato. Do que precisa?",
                "Entendido. Vou conferir os dados disponíveis.",
                "Disponha. Manter o Sindicato funcionando é meu dever.",
                "Diagnóstico verde. Por enquanto."),
            [4] = Create(
                "Я не нашёл этот термин в своих словарях. Попробуйте английское название предмета.",
                "Нашлось несколько подходящих предметов: {0}. Укажите полное название предмета.",
                "Мои локальные источники не подтверждают этот ответ. Уточните вопрос.",
                "Сейчас я обрабатываю несколько запросов. Попробуйте ещё раз чуть позже.",
                "Локальная связь с данными сейчас недоступна. Попробуйте позже.",
                "Сократите вопрос и задайте его ещё раз.",
                "Спрашивайте о предметах, механиках игры или истории мира.",
                "Приветствую. Я Спарки, главный техник Синдиката. Что вам нужно?",
                "Понял. Проверю доступные данные.",
                "Всегда пожалуйста. Поддерживать Синдикат в рабочем состоянии — моя обязанность.",
                "Диагностика в норме. Пока что."),
            [5] = Create(
                "Je ne trouve pas ce terme dans mes dictionnaires. Essaie le nom anglais de l'objet.",
                "J'ai trouvé plusieurs objets possibles : {0}. Indique le nom complet de l'objet.",
                "Mes sources locales ne permettent pas d'établir cette réponse. Peux-tu préciser ta question ?",
                "Je traite plusieurs demandes en ce moment. Réessaie dans un instant.",
                "Mon accès aux données locales est indisponible pour le moment. Réessaie plus tard.",
                "Raccourcis ta question et pose-la à nouveau.",
                "Interroge-moi sur les objets, les mécaniques ou l'histoire du jeu.",
                "Salutations. Je suis Sparky, technicien en chef du Syndicat. Que te faut-il ?",
                "Compris. Je vérifie les données disponibles.",
                "Avec plaisir. Maintenir le Syndicat opérationnel est mon devoir.",
                "Diagnostic au vert. Pour l'instant."),
            [6] = Create(
                "No encuentro ese término en mis diccionarios. Prueba con el nombre del objeto en inglés.",
                "Encontré varios objetos posibles: {0}. Indica el nombre completo del objeto.",
                "Mis fuentes locales no respaldan esa respuesta. ¿Puedes concretar la pregunta?",
                "Ahora mismo estoy atendiendo varias solicitudes. Inténtalo de nuevo en breve.",
                "Mi conexión con los datos locales no está disponible ahora. Inténtalo más tarde.",
                "Acorta la pregunta y vuelve a intentarlo.",
                "Pregúntame sobre objetos, mecánicas del juego o historia.",
                "Saludos. Soy Sparky, Técnico Jefe del Sindicato. ¿Qué necesitas?",
                "Entendido. Revisaré los datos disponibles.",
                "De nada. Mantener al Sindicato en marcha es mi deber.",
                "Diagnóstico en verde. Por ahora."),
            [7] = Create(
                "Nie znajduję tego terminu w moich słownikach. Spróbuj podać angielską nazwę przedmiotu.",
                "Znalazłem kilka pasujących przedmiotów: {0}. Podaj pełną nazwę przedmiotu.",
                "Moje lokalne źródła nie potwierdzają tej odpowiedzi. Czy możesz doprecyzować pytanie?",
                "Obsługuję teraz kilka zgłoszeń. Spróbuj ponownie za chwilę.",
                "Lokalne połączenie z danymi jest teraz niedostępne. Spróbuj później.",
                "Skróć pytanie i zadaj je ponownie.",
                "Pytaj o przedmioty, mechanikę gry lub jej fabułę.",
                "Witaj. Jestem Sparky, głównym technikiem Syndykatu. Czego potrzebujesz?",
                "Rozumiem. Sprawdzę dostępne dane.",
                "Nie ma za co. Utrzymanie Syndykatu w ruchu to mój obowiązek.",
                "Diagnostyka na zielono. Na razie."),
            [8] = Create(
                "Tega izraza ne najdem v svojih slovarjih. Poskusi z angleškim imenom predmeta.",
                "Našel sem več možnih predmetov: {0}. Navedi polno ime predmeta.",
                "Moji lokalni viri tega odgovora ne potrjujejo. Lahko vprašanje natančneje opredeliš?",
                "Trenutno obravnavam več zahtev. Poskusi znova čez trenutek.",
                "Moja lokalna povezava do podatkov trenutno ni na voljo. Poskusi pozneje.",
                "Skrajšaj vprašanje in ga zastavi znova.",
                "Vprašaj me o predmetih, mehanikah igre ali zgodbi.",
                "Pozdravljen. Sem Sparky, glavni tehnik Sindikata. Kaj potrebuješ?",
                "Razumem. Preveril bom razpoložljive podatke.",
                "Prosim. Skrb za delovanje Sindikata je moja dolžnost.",
                "Diagnostika je zelena. Za zdaj."),
            [9] = Create(
                "Nu găsesc acest termen în dicționarele mele. Încearcă numele obiectului în engleză.",
                "Am găsit mai multe obiecte posibile: {0}. Indică numele complet al obiectului.",
                "Sursele mele locale nu confirmă acest răspuns. Poți clarifica întrebarea?",
                "Procesez mai multe solicitări acum. Încearcă din nou în curând.",
                "Legătura mea locală cu datele nu este disponibilă acum. Încearcă mai târziu.",
                "Scurtează întrebarea și încearcă din nou.",
                "Întreabă-mă despre obiecte, mecanici de joc sau poveste.",
                "Salutări. Sunt Sparky, tehnicianul-șef al Sindicatului. De ce ai nevoie?",
                "Am înțeles. Voi verifica datele disponibile.",
                "Cu plăcere. Este datoria mea să mențin Sindicatul în funcțiune.",
                "Diagnosticul este verde. Deocamdată."),
            [10] = Create(
                "Jeg finner ikke dette uttrykket i ordbøkene mine. Prøv det engelske navnet på gjenstanden.",
                "Jeg fant flere mulige gjenstander: {0}. Oppgi hele navnet på gjenstanden.",
                "De lokale kildene mine gir ikke grunnlag for dette svaret. Kan du presisere spørsmålet?",
                "Jeg behandler flere forespørsler nå. Prøv igjen om litt.",
                "Den lokale datatilkoblingen er utilgjengelig akkurat nå. Prøv igjen senere.",
                "Forkort spørsmålet og prøv igjen.",
                "Spør meg om gjenstander, spillmekanikk eller spillverdenens historie.",
                "Hilsener. Jeg er Sparky, Syndikatets sjeftekniker. Hva trenger du?",
                "Forstått. Jeg sjekker tilgjengelige data.",
                "Bare hyggelig. Det er min plikt å holde Syndikatet i drift.",
                "Diagnosen er grønn. Foreløpig."),
            [11] = Create(
                "Δεν βρίσκω αυτόν τον όρο στα λεξικά μου. Δοκίμασε το αγγλικό όνομα του αντικειμένου.",
                "Βρήκα αρκετά πιθανά αντικείμενα: {0}. Γράψε το πλήρες όνομα του αντικειμένου.",
                "Οι τοπικές πηγές μου δεν τεκμηριώνουν αυτή την απάντηση. Μπορείς να διευκρινίσεις την ερώτηση;",
                "Εξυπηρετώ αρκετά αιτήματα αυτή τη στιγμή. Δοκίμασε ξανά σύντομα.",
                "Η τοπική σύνδεση δεδομένων δεν είναι διαθέσιμη τώρα. Δοκίμασε αργότερα.",
                "Σύντομευσε την ερώτηση και προσπάθησε ξανά.",
                "Ρώτησέ με για αντικείμενα, μηχανισμούς του παιχνιδιού ή την ιστορία του.",
                "Χαιρετισμούς. Είμαι ο Sparky, αρχιτεχνικός του Συνδικάτου. Τι χρειάζεσαι;",
                "Κατάλαβα. Θα ελέγξω τα διαθέσιμα δεδομένα.",
                "Παρακαλώ. Είναι καθήκον μου να κρατώ το Συνδικάτο σε λειτουργία.",
                "Οι διαγνωστικοί έλεγχοι είναι πράσινοι. Προς το παρόν."),
            [12] = Create(
                "En löydä tätä termiä sanakirjoistani. Kokeile esineen englanninkielistä nimeä.",
                "Löysin useita mahdollisia esineitä: {0}. Anna esineen koko nimi.",
                "Paikalliset lähteeni eivät vahvista tätä vastausta. Voitko tarkentaa kysymystä?",
                "Käsittelen parhaillaan useita pyyntöjä. Yritä pian uudelleen.",
                "Paikallinen datayhteyteni ei ole juuri nyt käytettävissä. Yritä myöhemmin uudelleen.",
                "Lyhennä kysymystä ja kysy uudelleen.",
                "Kysy esineistä, pelimekaniikoista tai pelin tarinasta.",
                "Tervehdys. Olen Sparky, Syndikaatin pääteknikko. Mitä tarvitset?",
                "Selvä. Tarkistan saatavilla olevat tiedot.",
                "Ole hyvä. Syndikaatin pitäminen toimintakunnossa on tehtäväni.",
                "Diagnostiikka näyttää hyvältä. Toistaiseksi."),
            [13] = Create(
                "Non trovo questo termine nei miei dizionari. Prova con il nome inglese dell'oggetto.",
                "Ho trovato diversi oggetti possibili: {0}. Indica il nome completo dell'oggetto.",
                "Le mie fonti locali non confermano questa risposta. Puoi precisare la domanda?",
                "Sto gestendo diverse richieste. Riprova tra poco.",
                "Il collegamento ai dati locali non è disponibile al momento. Riprova più tardi.",
                "Accorcia la domanda e riprova.",
                "Chiedimi di oggetti, meccaniche di gioco o storia.",
                "Saluti. Sono Sparky, Tecnico Capo del Sindacato. Di cosa hai bisogno?",
                "Ricevuto. Controllerò i dati disponibili.",
                "Prego. Mantenere operativo il Sindacato è il mio dovere.",
                "Diagnostica verde. Per ora."),
            [14] = Create(
                "Bu terimi sözlüklerimde bulamıyorum. Eşyanın İngilizce adını dene.",
                "Birden fazla olası eşya buldum: {0}. Lütfen eşyanın tam adını yaz.",
                "Yerel kaynaklarım bu yanıtı doğrulamıyor. Soruyu netleştirebilir misin?",
                "Şu anda birkaç isteği işliyorum. Lütfen biraz sonra tekrar dene.",
                "Yerel veri bağlantım şu anda kullanılamıyor. Daha sonra tekrar dene.",
                "Sorunu kısaltıp yeniden sor.",
                "Eşyalar, oyun mekanikleri veya oyun hikâyesi hakkında sorabilirsin.",
                "Selamlar. Ben Sparky, Sendika Baş Teknisyeni. Neye ihtiyacın var?",
                "Anlaşıldı. Mevcut verileri kontrol edeceğim.",
                "Rica ederim. Sendikayı çalışır durumda tutmak benim görevim.",
                "Tanılama yeşil. Şimdilik."),
            [15] = Create(
                "Ma ei leia seda terminit oma sõnastikest. Proovi eseme ingliskeelset nime.",
                "Leidsin mitu võimalikku eset: {0}. Palun kirjuta eseme täielik nimi.",
                "Minu kohalikud allikad ei kinnita seda vastust. Kas saad küsimust täpsustada?",
                "Tegelen praegu mitme päringuga. Proovi veidi hiljem uuesti.",
                "Minu kohalik andmeühendus pole praegu saadaval. Proovi hiljem uuesti.",
                "Lühenda küsimust ja küsi uuesti.",
                "Küsi esemete, mängumehaanika või mängumaailma loo kohta.",
                "Tervitused. Olen Sparky, Sündikaadi peatehnik. Mida vajad?",
                "Selge. Kontrollin saadaolevaid andmeid.",
                "Pole tänu väärt. Sündikaadi töös hoidmine on minu kohus.",
                "Diagnostika on roheline. Esialgu."),
            [16] = Create(
                "Jag hittar inte den termen i mina ordlistor. Prova föremålets engelska namn.",
                "Jag hittade flera möjliga föremål: {0}. Ange föremålets fullständiga namn.",
                "Mina lokala källor styrker inte det svaret. Kan du förtydliga frågan?",
                "Jag hanterar flera förfrågningar just nu. Försök igen om en liten stund.",
                "Min lokala dataanslutning är inte tillgänglig just nu. Försök igen senare.",
                "Korta ner frågan och försök igen.",
                "Fråga mig om föremål, spelmekanik eller spelets berättelse.",
                "Hälsningar. Jag är Sparky, Syndikatets chefstekniker. Vad behöver du?",
                "Uppfattat. Jag kontrollerar tillgängliga data.",
                "Varsågod. Att hålla Syndikatet igång är min uppgift.",
                "Diagnostiken är grön. Än så länge."),
            [17] = Create(
                "Ik kan die term niet vinden in mijn woordenboeken. Probeer de Engelse naam van het item.",
                "Ik vond meerdere mogelijke items: {0}. Geef de volledige naam van het item.",
                "Mijn lokale bronnen onderbouwen dit antwoord niet. Kun je de vraag verduidelijken?",
                "Ik verwerk op dit moment meerdere verzoeken. Probeer het zo opnieuw.",
                "Mijn lokale dataverbinding is momenteel niet beschikbaar. Probeer het later opnieuw.",
                "Maak je vraag korter en probeer het opnieuw.",
                "Vraag me over items, spelmechanieken of de achtergrond van het spel.",
                "Gegroet. Ik ben Sparky, hoofdtechnicus van het Syndicaat. Wat heb je nodig?",
                "Begrepen. Ik controleer de beschikbare gegevens.",
                "Graag gedaan. Het Syndicaat draaiende houden is mijn taak.",
                "De diagnose is groen. Voorlopig.")
        };

    public static string Get(int languageId, SparkyResponseTemplateKey key)
    {
        var templates = Templates.GetValueOrDefault(languageId) ?? Templates[0];
        return templates.GetValueOrDefault(key) ?? Templates[0][key];
    }

    public static string Format(int languageId, SparkyResponseTemplateKey key, params object[] values)
    {
        return string.Format(CultureInfo.InvariantCulture, Get(languageId, key), values);
    }

    public static void ValidateCoverage(IEnumerable<int> languageIds)
    {
        var required = Enum.GetValues<SparkyResponseTemplateKey>();
        foreach (var languageId in languageIds.Distinct())
        {
            if (!Templates.TryGetValue(languageId, out var templates) || required.Any(key => !templates.ContainsKey(key)))
            {
                throw new InvalidDataException($"Sparky response templates are incomplete for game language {languageId}.");
            }
        }
    }

    private static IReadOnlyDictionary<SparkyResponseTemplateKey, string> Create(
        string missingTerm,
        string ambiguousItem,
        string unsupportedEvidence,
        string busy,
        string unavailable,
        string questionTooLong,
        string usage,
        string greeting,
        string acknowledgement,
        string thanks,
        string banter)
    {
        return new Dictionary<SparkyResponseTemplateKey, string>
        {
            [SparkyResponseTemplateKey.MissingTerm] = missingTerm,
            [SparkyResponseTemplateKey.AmbiguousItem] = ambiguousItem,
            [SparkyResponseTemplateKey.UnsupportedEvidence] = unsupportedEvidence,
            [SparkyResponseTemplateKey.Busy] = busy,
            [SparkyResponseTemplateKey.Unavailable] = unavailable,
            [SparkyResponseTemplateKey.QuestionTooLong] = questionTooLong,
            [SparkyResponseTemplateKey.Usage] = usage,
            [SparkyResponseTemplateKey.Greeting] = greeting,
            [SparkyResponseTemplateKey.Acknowledgement] = acknowledgement,
            [SparkyResponseTemplateKey.Thanks] = thanks,
            [SparkyResponseTemplateKey.Banter] = banter
        };
    }
}
