namespace Hlibz.Results.AspNetCore;

/// <summary>
/// The message sent for each outcome when the result (or the auth gate) carries none of its own.
/// </summary>
public sealed class ResultMessages
{
    /// <summary>
    /// Gets or sets the message of a successful envelope.
    /// </summary>
    public string Success { get; set; } = "Success.";

    /// <summary>
    /// Gets or sets the message of a 400.
    /// </summary>
    public string BadRequest { get; set; } = "Bad request.";

    /// <summary>
    /// Gets or sets the message of a validation failure without one of its own.
    /// </summary>
    public string Invalid { get; set; } = Result.DefaultValidationMessage;

    /// <summary>
    /// Gets or sets the message of a 404.
    /// </summary>
    public string NotFound { get; set; } = "Resource was not found.";

    /// <summary>
    /// Gets or sets the message of a 401.
    /// </summary>
    public string Unauthorized { get; set; } = "Authentication is required.";

    /// <summary>
    /// Gets or sets the message of a 403.
    /// </summary>
    public string Forbidden { get; set; } = "Access to the resource is forbidden.";

    /// <summary>
    /// Gets or sets the message of a 409.
    /// </summary>
    public string Conflict { get; set; } =
        "The request conflicts with the current state of the resource.";

    /// <summary>
    /// Gets or sets the message of a 503.
    /// </summary>
    public string Unavailable { get; set; } =
        "The service is temporarily unavailable. Please retry later.";

    /// <summary>
    /// Gets the default message for <paramref name="status"/>.
    /// </summary>
    /// <param name="status">The outcome.</param>
    /// <returns>The message.</returns>
    public string For(ResultStatus status) => status switch
    {
        ResultStatus.Success => Success,
        ResultStatus.Error => BadRequest,
        ResultStatus.Invalid => Invalid,
        ResultStatus.NotFound => NotFound,
        ResultStatus.Unauthorized => Unauthorized,
        ResultStatus.Forbidden => Forbidden,
        ResultStatus.Conflict => Conflict,
        ResultStatus.Unavailable => Unavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
