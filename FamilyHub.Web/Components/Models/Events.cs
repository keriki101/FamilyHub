    public class Event {
        public Guid Id { get; set; }
        public Guid PersonId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Color { get; set; } = string.Empty;
        public Guid? RecurrenceSeriesId { get; set; }
        public DateTime? RecurrenceStart { get; set; }
        public DateTime? RecurrenceUntil { get; set; }
        public int? RecurrenceDays { get; set; }
        public DateTime? RecurrenceOccurrenceDate { get; set; }
        public bool RecurrenceIsOverride { get; set; }
        public bool RecurrenceIsDeleted { get; set; }
        public bool IsWorkEvent { get; set; }
    }