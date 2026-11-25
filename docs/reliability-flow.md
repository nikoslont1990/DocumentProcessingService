HTTP API και παραλαβή folder.
Προσωρινή αποθήκευση στο filesystem.
Αποθήκευση στο Blob Storage.
Αποθήκευση metadata και Outbox record στη βάση.
Outbox worker και polling.
Δημοσίευση στο Azure Service Bus.
Subscriber worker, επεξεργασία και αποθήκευση αποτελεσμάτων στη βάση.

1. Duplicate delivery: Αν λάβεις δύο φορές το ίδιο MessageId, θα δημιουργηθούν δύο business records ή μόνο ένα;
2. Failure πριν το commit: Αν αποτύχει η business logic μετά την εισαγωγή του Inbox record, θα μείνει καταχωρισμένο το Inbox record;
3. Concurrent delivery: Αν δύο executions προσπαθήσουν ταυτόχρονα να εισαγάγουν το ίδιο Inbox identity, ποιο είναι το τελικό σημείο που εγγυάται τη μοναδικότητα;
4. Failure μετά το commit: Αν η transaction γίνει commit αλλά αποτύχει το Service Bus Complete, τι θα συμβεί όταν παραδοθεί ξανά το μήνυμα;


Ερώτηση	              Αξιολόγηση	Παρατήρηση

Duplicate delivery	    Σωστή	    Unique constraint ως τελική εγγύηση
Failure πριν το commit	Σωστή	    Rollback και των δύο αλλαγών
Concurrent delivery	    Σωστή	    Η βάση προστατεύει από race conditions
Failure μετά το commit	Σωστή	    Το duplicate αναγνωρίζεται και γίνεται Complete

Υπάρχουν δύο τεχνικές λεπτομέρειες που θέλω να προσέξεις:

Α. Check-then-act δεν αρκεί.

Αν κάνεις πρώτα SELECT για να δεις αν υπάρχει το Inbox record και μετά INSERT, δύο executions μπορούν να περάσουν ταυτόχρονα τον έλεγχο.

Το unique constraint είναι αυτό που προστατεύει πραγματικά τη μοναδικότητα.

Β. Πρόσεξε το duplicate handling.

Αν το INSERT αποτύχει λόγω unique constraint, πρέπει να βεβαιωθείς ότι πρόκειται πράγματι για duplicate και όχι για κάποιο άλλο database error.

Επίσης, αν χρησιμοποιείς EF Core transactions, έλεγξε πώς συμπεριφέρεται ο provider σου μετά από constraint violation και αν χρειάζεται rollback πριν συνεχίσεις.



!!!!!!
Service Bus duplicate detection window: Για πόσο διάστημα το Service Bus αναγνωρίζει ένα ήδη δημοσιευμένο MessageId.
Inbox retention: Για πόσο διάστημα διατηρείς τα processed message IDs στη βάση.
Business requirements: Μπορεί ένα event να επανεμφανιστεί μετά από εβδομάδες ή μήνες μέσω replay, manual recovery ή άλλου μηχανισμού;

Failure	Τι μένει πίσω
Αποτυχία πριν ή κατά το Blob upload	Προσωρινά ή μερικώς αποθηκευμένα αρχεία
Blob upload πέτυχε, database transaction απέτυχε	Orphaned Blob
Metadata και Outbox αποθηκεύτηκαν, αλλά η απάντηση HTTP χάθηκε	Ο client μπορεί να επαναλάβει το ίδιο request