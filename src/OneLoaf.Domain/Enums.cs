namespace OneLoaf.Domain;

// Closed sets of states and kinds that the application logic depends on.
// They are stored as snake_case text (e.g. InProgress -> 'in_progress') and guarded by CHECK constraints.
// Open, descriptive classifications with attributes (units, stage types, ingredient categories,
// flour types, currencies) are reference tables instead, see Entities/Reference.cs.

public enum UserRole { User, Admin }

public enum ProofMode { Warm, Cold }

public enum BatchStatus { Planned, InProgress, Done, Cancelled }

public enum BatchSource { Manual, Assistant }

public enum StageStatus { Pending, Running, Done, Skipped }

public enum FeedingMode { Regular, Weekly, Fridge }

public enum ReminderKind { StarterFeeding, BeforeKneading, StageStart }

public enum ReminderChannel { Push, Email }

public enum ReminderStatus { Scheduled, Sent, Cancelled, Failed }

public enum ChatRole { User, Assistant, Tool }

public enum PublicationStatus { Published, Unpublished, Removed }

public enum ReactionType { Like, Love, WantToBake }

public enum ReportStatus { Open, Resolved, Rejected }
