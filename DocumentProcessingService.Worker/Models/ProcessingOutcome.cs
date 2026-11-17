using System;

public enum ProcessingOutcome
{
    Completed,
    Duplicate,
    MissingMessageId,
    InvalidPayload,
    DocumentNotFoundRetryable
}