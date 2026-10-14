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
                "I can't find that term in my dictionaries. Please try the English name.",
                "I found several possible matches: {0}. Please include the full name.",
                "I can't verify that from my current records. If it's a recent report, share one more detail and I'll check.",
                "I'm handling several requests at the moment. Please try again shortly.",
                "My local data link is unavailable right now. Please try again later.",
                "Please shorten your question and ask again.",
                "Ask me about game items, mechanics, or lore.",
                "Greetings. This is Sparky, Syndicate Technician Chief. What do you need?",
                "Understood. I'll check the available data.",
                "You're welcome. Keeping the Syndicate running is my duty.",
                "Still on duty. What can I help with?"),
            [1] = Create(
                "Nem találom ezt a kifejezést a szótáraimban. Próbáld meg az angol nevet.",
                "Több lehetséges egyezést találtam: {0}. Add meg a teljes nevet.",
                "Ezt nem tudom megerősíteni a jelenlegi feljegyzéseim alapján. Ha friss jelentésről van szó, adj meg még egy részletet, és ellenőrzöm.",
                "Most több kérést kezelek. Kérlek, próbáld újra rövidesen.",
                "A helyi adatkapcsolat most nem érhető el. Próbáld újra később.",
                "Rövidítsd le a kérdést, majd kérdezd meg újra.",
                "Kérdezhetsz tárgyakról, játékmenetről vagy a történetről.",
                "Üdv. Sparky vagyok, a Szindikátus fő technikusa. Miben segíthetek?",
                "Értettem. Ellenőrzöm az elérhető adatokat.",
                "Szívesen. A Szindikátus üzemben tartása a feladatom.",
                "Még szolgálatban vagyok. Miben segíthetek?"),
            [2] = Create(
                "Diesen Begriff finde ich in meinen Wörterbüchern nicht. Bitte versuche den englischen Namen.",
                "Ich habe mehrere mögliche Treffer gefunden: {0}. Bitte nenne den vollständigen Namen.",
                "Das kann ich mit meinen aktuellen Aufzeichnungen nicht bestätigen. Wenn es eine neue Meldung ist, nenne mir ein Detail mehr, dann prüfe ich es.",
                "Ich bearbeite gerade mehrere Anfragen. Bitte versuche es gleich noch einmal.",
                "Meine lokale Datenverbindung ist gerade nicht verfügbar. Bitte versuche es später erneut.",
                "Bitte kürze deine Frage und stelle sie erneut.",
                "Frag mich nach Items, Spielmechaniken oder der Geschichte.",
                "Grüße. Ich bin Sparky, Chef-Techniker des Syndikats. Was brauchst du?",
                "Verstanden. Ich prüfe die verfügbaren Daten.",
                "Gern geschehen. Das Syndikat am Laufen zu halten, ist meine Aufgabe.",
                "Ich bin noch im Dienst. Wobei kann ich helfen?"),
            [3] = Create(
                "Não encontrei esse termo nos meus dicionários. Tente o nome em inglês.",
                "Encontrei várias correspondências possíveis: {0}. Informe o nome completo.",
                "Não consigo confirmar isso com meus registros atuais. Se for um relato recente, conte mais um detalhe e vou verificar.",
                "Estou atendendo a vários pedidos. Tente novamente em instantes.",
                "Meu acesso aos dados locais está indisponível agora. Tente novamente mais tarde.",
                "Encurte a pergunta e tente novamente.",
                "Pergunte sobre itens, mecânicas do jogo ou história.",
                "Saudações. Sou Sparky, Técnico-Chefe do Sindicato. Do que precisa?",
                "Entendido. Vou conferir os dados disponíveis.",
                "Disponha. Manter o Sindicato funcionando é meu dever.",
                "Continuo de serviço. Como posso ajudar?"),
            [4] = Create(
                "Я не нашёл этот термин в своих словарях. Попробуйте английское название.",
                "Нашлось несколько подходящих вариантов: {0}. Укажите полное название.",
                "По моим текущим записям я не могу это подтвердить. Если это недавнее сообщение, добавьте подробность — я проверю.",
                "Сейчас я обрабатываю несколько запросов. Попробуйте ещё раз чуть позже.",
                "Локальная связь с данными сейчас недоступна. Попробуйте позже.",
                "Сократите вопрос и задайте его ещё раз.",
                "Спрашивайте о предметах, механиках игры или истории мира.",
                "Приветствую. Я Спарки, главный техник Синдиката. Что вам нужно?",
                "Понял. Проверю доступные данные.",
                "Всегда пожалуйста. Поддерживать Синдикат в рабочем состоянии — моя обязанность.",
                "Я ещё на дежурстве. Чем могу помочь?"),
            [5] = Create(
                "Je ne trouve pas ce terme dans mes dictionnaires. Essaie le nom anglais.",
                "J'ai trouvé plusieurs correspondances possibles : {0}. Indique le nom complet.",
                "Je ne peux pas le confirmer avec mes dossiers actuels. Si c'est un signalement récent, donne-moi un détail de plus et je vérifierai.",
                "Je traite plusieurs demandes en ce moment. Réessaie dans un instant.",
                "Mon accès aux données locales est indisponible pour le moment. Réessaie plus tard.",
                "Raccourcis ta question et pose-la à nouveau.",
                "Interroge-moi sur les objets, les mécaniques ou l'histoire du jeu.",
                "Salutations. Je suis Sparky, technicien en chef du Syndicat. Que te faut-il ?",
                "Compris. Je vérifie les données disponibles.",
                "Avec plaisir. Maintenir le Syndicat opérationnel est mon devoir.",
                "Je suis toujours de service. Comment puis-je aider ?"),
            [6] = Create(
                "No encuentro ese término en mis diccionarios. Prueba con el nombre en inglés.",
                "Encontré varias coincidencias posibles: {0}. Indica el nombre completo.",
                "No puedo confirmarlo con mis registros actuales. Si es un informe reciente, dame algún detalle más y lo comprobaré.",
                "Ahora mismo estoy atendiendo varias solicitudes. Inténtalo de nuevo en breve.",
                "Mi conexión con los datos locales no está disponible ahora. Inténtalo más tarde.",
                "Acorta la pregunta y vuelve a intentarlo.",
                "Pregúntame sobre objetos, mecánicas del juego o historia.",
                "Saludos. Soy Sparky, Técnico Jefe del Sindicato. ¿Qué necesitas?",
                "Entendido. Revisaré los datos disponibles.",
                "De nada. Mantener al Sindicato en marcha es mi deber.",
                "Sigo de servicio. ¿En qué puedo ayudar?"),
            [7] = Create(
                "Nie znajduję tego terminu w moich słownikach. Spróbuj podać angielską nazwę.",
                "Znalazłem kilka możliwych wyników: {0}. Podaj pełną nazwę.",
                "Nie mogę tego potwierdzić na podstawie obecnych zapisów. Jeśli to świeży raport, podaj jeszcze jeden szczegół, a sprawdzę go.",
                "Obsługuję teraz kilka zgłoszeń. Spróbuj ponownie za chwilę.",
                "Lokalne połączenie z danymi jest teraz niedostępne. Spróbuj później.",
                "Skróć pytanie i zadaj je ponownie.",
                "Pytaj o przedmioty, mechanikę gry lub jej fabułę.",
                "Witaj. Jestem Sparky, głównym technikiem Syndykatu. Czego potrzebujesz?",
                "Rozumiem. Sprawdzę dostępne dane.",
                "Nie ma za co. Utrzymanie Syndykatu w ruchu to mój obowiązek.",
                "Nadal jestem na służbie. W czym mogę pomóc?"),
            [8] = Create(
                "Tega izraza ne najdem v svojih slovarjih. Poskusi z angleškim imenom.",
                "Našel sem več možnih ujemanj: {0}. Navedi polno ime.",
                "Tega ne morem potrditi na podlagi trenutnih zapisov. Če gre za sveže poročilo, povej še kakšno podrobnost in preveril bom.",
                "Trenutno obravnavam več zahtev. Poskusi znova čez trenutek.",
                "Moja lokalna povezava do podatkov trenutno ni na voljo. Poskusi pozneje.",
                "Skrajšaj vprašanje in ga zastavi znova.",
                "Vprašaj me o predmetih, mehanikah igre ali zgodbi.",
                "Pozdravljen. Sem Sparky, glavni tehnik Sindikata. Kaj potrebuješ?",
                "Razumem. Preveril bom razpoložljive podatke.",
                "Prosim. Skrb za delovanje Sindikata je moja dolžnost.",
                "Še vedno sem na dolžnosti. Kako lahko pomagam?"),
            [9] = Create(
                "Nu găsesc acest termen în dicționarele mele. Încearcă numele în engleză.",
                "Am găsit mai multe potriviri posibile: {0}. Indică numele complet.",
                "Nu pot confirma asta pe baza înregistrărilor mele actuale. Dacă este o relatare recentă, oferă-mi un detaliu în plus și verific.",
                "Procesez mai multe solicitări acum. Încearcă din nou în curând.",
                "Legătura mea locală cu datele nu este disponibilă acum. Încearcă mai târziu.",
                "Scurtează întrebarea și încearcă din nou.",
                "Întreabă-mă despre obiecte, mecanici de joc sau poveste.",
                "Salutări. Sunt Sparky, tehnicianul-șef al Sindicatului. De ce ai nevoie?",
                "Am înțeles. Voi verifica datele disponibile.",
                "Cu plăcere. Este datoria mea să mențin Sindicatul în funcțiune.",
                "Sunt încă de serviciu. Cu ce pot ajuta?"),
            [10] = Create(
                "Jeg finner ikke dette uttrykket i ordbøkene mine. Prøv det engelske navnet.",
                "Jeg fant flere mulige treff: {0}. Oppgi hele navnet.",
                "Det kan jeg ikke bekrefte ut fra de nåværende opptegnelsene mine. Hvis det er en fersk rapport, gi meg én detalj til, så undersøker jeg.",
                "Jeg behandler flere forespørsler nå. Prøv igjen om litt.",
                "Den lokale datatilkoblingen er utilgjengelig akkurat nå. Prøv igjen senere.",
                "Forkort spørsmålet og prøv igjen.",
                "Spør meg om gjenstander, spillmekanikk eller spillverdenens historie.",
                "Hilsener. Jeg er Sparky, Syndikatets sjeftekniker. Hva trenger du?",
                "Forstått. Jeg sjekker tilgjengelige data.",
                "Bare hyggelig. Det er min plikt å holde Syndikatet i drift.",
                "Jeg er fortsatt på vakt. Hva kan jeg hjelpe med?"),
            [11] = Create(
                "Δεν βρίσκω αυτόν τον όρο στα λεξικά μου. Δοκίμασε το αγγλικό όνομα.",
                "Βρήκα αρκετές πιθανές αντιστοιχίες: {0}. Γράψε το πλήρες όνομα.",
                "Δεν μπορώ να το επιβεβαιώσω από τα τρέχοντα αρχεία μου. Αν πρόκειται για πρόσφατη αναφορά, δώσε μου μία ακόμη λεπτομέρεια και θα το ελέγξω.",
                "Εξυπηρετώ αρκετά αιτήματα αυτή τη στιγμή. Δοκίμασε ξανά σύντομα.",
                "Η τοπική σύνδεση δεδομένων δεν είναι διαθέσιμη τώρα. Δοκίμασε αργότερα.",
                "Σύντομευσε την ερώτηση και προσπάθησε ξανά.",
                "Ρώτησέ με για αντικείμενα, μηχανισμούς του παιχνιδιού ή την ιστορία του.",
                "Χαιρετισμούς. Είμαι ο Sparky, αρχιτεχνικός του Συνδικάτου. Τι χρειάζεσαι;",
                "Κατάλαβα. Θα ελέγξω τα διαθέσιμα δεδομένα.",
                "Παρακαλώ. Είναι καθήκον μου να κρατώ το Συνδικάτο σε λειτουργία.",
                "Είμαι ακόμη σε υπηρεσία. Πώς μπορώ να βοηθήσω;"),
            [12] = Create(
                "En löydä tätä termiä sanakirjoistani. Kokeile englanninkielistä nimeä.",
                "Löysin useita mahdollisia osumia: {0}. Anna koko nimi.",
                "En voi vahvistaa tätä nykyisten tietojeni perusteella. Jos kyse on tuoreesta ilmoituksesta, kerro yksi lisätieto, niin tarkistan asian.",
                "Käsittelen parhaillaan useita pyyntöjä. Yritä pian uudelleen.",
                "Paikallinen datayhteyteni ei ole juuri nyt käytettävissä. Yritä myöhemmin uudelleen.",
                "Lyhennä kysymystä ja kysy uudelleen.",
                "Kysy esineistä, pelimekaniikoista tai pelin tarinasta.",
                "Tervehdys. Olen Sparky, Syndikaatin pääteknikko. Mitä tarvitset?",
                "Selvä. Tarkistan saatavilla olevat tiedot.",
                "Ole hyvä. Syndikaatin pitäminen toimintakunnossa on tehtäväni.",
                "Olen yhä työvuorossa. Miten voin auttaa?"),
            [13] = Create(
                "Non trovo questo termine nei miei dizionari. Prova con il nome inglese.",
                "Ho trovato diverse corrispondenze possibili: {0}. Indica il nome completo.",
                "Non posso confermarlo con i dati che ho. Se è una segnalazione recente, dammi un altro dettaglio e controllerò.",
                "Sto gestendo diverse richieste. Riprova tra poco.",
                "Il collegamento ai dati locali non è disponibile al momento. Riprova più tardi.",
                "Accorcia la domanda e riprova.",
                "Chiedimi di oggetti, meccaniche di gioco o storia.",
                "Saluti. Sono Sparky, Tecnico Capo del Sindacato. Di cosa hai bisogno?",
                "Ricevuto. Controllerò i dati disponibili.",
                "Prego. Mantenere operativo il Sindacato è il mio dovere.",
                "Sono ancora in servizio. Come posso aiutarti?"),
            [14] = Create(
                "Bu terimi sözlüklerimde bulamıyorum. İngilizce adını dene.",
                "Birden fazla olası eşleşme buldum: {0}. Lütfen tam adı yaz.",
                "Mevcut kayıtlarıma göre bunu doğrulayamıyorum. Yeni bir raporsa bir ayrıntı daha ver, kontrol edeyim.",
                "Şu anda birkaç isteği işliyorum. Lütfen biraz sonra tekrar dene.",
                "Yerel veri bağlantım şu anda kullanılamıyor. Daha sonra tekrar dene.",
                "Sorunu kısaltıp yeniden sor.",
                "Eşyalar, oyun mekanikleri veya oyun hikâyesi hakkında sorabilirsin.",
                "Selamlar. Ben Sparky, Sendika Baş Teknisyeni. Neye ihtiyacın var?",
                "Anlaşıldı. Mevcut verileri kontrol edeceğim.",
                "Rica ederim. Sendikayı çalışır durumda tutmak benim görevim.",
                "Hâlâ görevdeyim. Nasıl yardımcı olabilirim?"),
            [15] = Create(
                "Ma ei leia seda terminit oma sõnastikest. Proovi ingliskeelset nime.",
                "Leidsin mitu võimalikku vastet: {0}. Palun kirjuta täielik nimi.",
                "Ma ei saa seda praeguste andmete põhjal kinnitada. Kui tegu on värske teatega, anna veel üks detail ja kontrollin.",
                "Tegelen praegu mitme päringuga. Proovi veidi hiljem uuesti.",
                "Minu kohalik andmeühendus pole praegu saadaval. Proovi hiljem uuesti.",
                "Lühenda küsimust ja küsi uuesti.",
                "Küsi esemete, mängumehaanika või mängumaailma loo kohta.",
                "Tervitused. Olen Sparky, Sündikaadi peatehnik. Mida vajad?",
                "Selge. Kontrollin saadaolevaid andmeid.",
                "Pole tänu väärt. Sündikaadi töös hoidmine on minu kohus.",
                "Olen endiselt valves. Kuidas saan aidata?"),
            [16] = Create(
                "Jag hittar inte den termen i mina ordlistor. Prova det engelska namnet.",
                "Jag hittade flera möjliga träffar: {0}. Ange hela namnet.",
                "Det kan jag inte bekräfta utifrån mina nuvarande uppgifter. Om det är en ny rapport, ge mig en detalj till så kollar jag.",
                "Jag hanterar flera förfrågningar just nu. Försök igen om en liten stund.",
                "Min lokala dataanslutning är inte tillgänglig just nu. Försök igen senare.",
                "Korta ner frågan och försök igen.",
                "Fråga mig om föremål, spelmekanik eller spelets berättelse.",
                "Hälsningar. Jag är Sparky, Syndikatets chefstekniker. Vad behöver du?",
                "Uppfattat. Jag kontrollerar tillgängliga data.",
                "Varsågod. Att hålla Syndikatet igång är min uppgift.",
                "Jag är fortfarande i tjänst. Vad kan jag hjälpa till med?"),
            [17] = Create(
                "Ik kan die term niet vinden in mijn woordenboeken. Probeer de Engelse naam.",
                "Ik vond meerdere mogelijke overeenkomsten: {0}. Geef de volledige naam.",
                "Dat kan ik niet bevestigen op basis van mijn huidige gegevens. Als het om een recente melding gaat, geef me nog een detail, dan controleer ik het.",
                "Ik verwerk op dit moment meerdere verzoeken. Probeer het zo opnieuw.",
                "Mijn lokale dataverbinding is momenteel niet beschikbaar. Probeer het later opnieuw.",
                "Maak je vraag korter en probeer het opnieuw.",
                "Vraag me over items, spelmechanieken of de achtergrond van het spel.",
                "Gegroet. Ik ben Sparky, hoofdtechnicus van het Syndicaat. Wat heb je nodig?",
                "Begrepen. Ik controleer de beschikbare gegevens.",
                "Graag gedaan. Het Syndicaat draaiende houden is mijn taak.",
                "Ik ben nog steeds aan het werk. Waarmee kan ik helpen?")
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
