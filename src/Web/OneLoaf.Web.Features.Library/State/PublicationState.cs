using Fluxor;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Library.State;

/// <summary>Publication page (US-40–42, LR-11): the snapshot, reactions and comments; counts always come from the API.</summary>
[FeatureState(Name = "Publication")]
public sealed record PublicationState(
    Guid? Id,
    Loadable<PublishedRecipeDto> Publication,
    Loadable<PagedList<CommentDto>> Comments,
    string? Running)
{
    private PublicationState()
        : this(null, Loadable<PublishedRecipeDto>.Idle, Loadable<PagedList<CommentDto>>.Idle, null)
    {
    }

    /// <summary>The own ingredients, loaded when the recipe is about to be saved; null before that.</summary>
    public IReadOnlyList<IngredientDto>? MyIngredients { get; init; }
}

/// <summary>Intent: open a publication.</summary>
public sealed record OpenPublication(Guid Id);

/// <summary>Result: publication.</summary>
public sealed record PublicationLoaded(Guid Id, Loadable<PublishedRecipeDto> Publication);

/// <summary>Result: comments (first page, or the next one appended when <paramref name="Append"/>).</summary>
public sealed record CommentsLoaded(Guid Id, Loadable<PagedList<CommentDto>> Comments);

/// <summary>Intent: next page of comments.</summary>
public sealed record LoadMoreComments;

/// <summary>Intent: add a comment.</summary>
public sealed record AddComment(string Body);

/// <summary>Result: comment added.</summary>
public sealed record CommentAdded(CommentDto Comment);

/// <summary>Intent: remove an own comment.</summary>
public sealed record RemoveComment(Guid CommentId);

/// <summary>Intent: set (or with null remove) the own reaction.</summary>
public sealed record SetReaction(string? Reaction);

/// <summary>Intent: load the own ingredients, to match them to those of the recipe before it is saved.</summary>
public sealed record PrepareCopy;

/// <summary>Result: the own ingredients (empty when they could not be loaded: everything is added as new).</summary>
public sealed record CopyIngredientsLoaded(IReadOnlyList<IngredientDto> Ingredients);

/// <summary>Intent: save the recipe to the own recipes with the chosen ingredients.</summary>
public sealed record CopyPublication(CopyPublicationRequest Request);

/// <summary>Result: copied; the page opens the new recipe.</summary>
public sealed record PublicationCopied(Guid RecipeId);

/// <summary>Intent: report the publication or one of its comments.</summary>
public sealed record ReportContent(string Reason, Guid? CommentId);

/// <summary>Result: report sent.</summary>
public sealed record ContentReported;

/// <summary>Intent: unpublish (author only).</summary>
public sealed record UnpublishPublication;

/// <summary>Result: unpublished; the page returns to the library.</summary>
public sealed record PublicationUnpublished(Guid Id);

/// <summary>Result: a command finished (successfully or not) — <see cref="Error"/> tells which.</summary>
public sealed record PublicationCommandDone(string Command, ApiError? Error);

/// <summary>Reducers of <see cref="PublicationState"/>.</summary>
public static class PublicationReducers
{
    /// <summary>Opening another publication starts empty; the same one reloads in place.</summary>
    [ReducerMethod]
    public static PublicationState OnOpen(PublicationState state, OpenPublication action) =>
        state.Id == action.Id
            ? state with { Publication = state.Publication.Reload(), Comments = state.Comments.Reload() }
            : new PublicationState(action.Id, new Loadable<PublishedRecipeDto>.Loading(null), new Loadable<PagedList<CommentDto>>.Loading(null), null);

    /// <summary>Publication.</summary>
    [ReducerMethod]
    public static PublicationState OnLoaded(PublicationState state, PublicationLoaded action) =>
        action.Id == state.Id ? state with { Publication = action.Publication } : state;

    /// <summary>Comments.</summary>
    [ReducerMethod]
    public static PublicationState OnComments(PublicationState state, CommentsLoaded action) =>
        action.Id == state.Id ? state with { Comments = action.Comments, Running = state.Running == nameof(LoadMoreComments) ? null : state.Running } : state;

    /// <summary>Commands mark what is running.</summary>
    [ReducerMethod(typeof(LoadMoreComments))]
    public static PublicationState OnMore(PublicationState state) => state with { Running = nameof(LoadMoreComments) };

    /// <summary>Adding a comment.</summary>
    [ReducerMethod(typeof(AddComment))]
    public static PublicationState OnAdd(PublicationState state) => state with { Running = nameof(AddComment) };

    /// <summary>Reacting.</summary>
    [ReducerMethod(typeof(SetReaction))]
    public static PublicationState OnReact(PublicationState state) => state with { Running = nameof(SetReaction) };

    /// <summary>The own ingredients arrived for the matching dialog.</summary>
    [ReducerMethod]
    public static PublicationState OnCopyIngredients(PublicationState state, CopyIngredientsLoaded action) =>
        state with { MyIngredients = action.Ingredients };

    /// <summary>The dialog opens again: the own ingredients are loaded afresh.</summary>
    [ReducerMethod(typeof(PrepareCopy))]
    public static PublicationState OnPrepareCopy(PublicationState state) => state with { MyIngredients = null };

    /// <summary>Copying.</summary>
    [ReducerMethod(typeof(CopyPublication))]
    public static PublicationState OnCopy(PublicationState state) => state with { Running = nameof(CopyPublication) };

    /// <summary>Reporting.</summary>
    [ReducerMethod(typeof(ReportContent))]
    public static PublicationState OnReport(PublicationState state) => state with { Running = nameof(ReportContent) };

    /// <summary>Unpublishing.</summary>
    [ReducerMethod(typeof(UnpublishPublication))]
    public static PublicationState OnUnpublish(PublicationState state) => state with { Running = nameof(UnpublishPublication) };

    /// <summary>A command finished.</summary>
    [ReducerMethod(typeof(PublicationCommandDone))]
    public static PublicationState OnDone(PublicationState state) => state with { Running = null };

    /// <summary>The new comment goes on top; the count is reloaded from the API with the publication.</summary>
    [ReducerMethod]
    public static PublicationState OnCommentAdded(PublicationState state, CommentAdded action) =>
        state.Comments.Value is { } comments
            ? state with { Comments = new Loadable<PagedList<CommentDto>>.Loaded(comments with { Items = comments.Items.Insert(0, action.Comment) }) }
            : state;

    /// <summary>The library list shows the change after a reload.</summary>
    [ReducerMethod(typeof(PublicationUnpublished))]
    public static LibraryState ListAfterUnpublish(LibraryState state) => state with { List = Loadable<PagedList<LibraryItemDto>>.Idle };
}

/// <summary>Side effects of the publication page.</summary>
public sealed class PublicationEffects(ILibraryApi api, IState<PublicationState> state)
{
    /// <summary>Publication and comments in parallel.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenPublication action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var comments = api.ListCommentsAsync(action.Id, 1);
        dispatcher.Dispatch(new PublicationLoaded(action.Id, (await api.GetAsync(action.Id)).ToLoadable()));
        dispatcher.Dispatch(new CommentsLoaded(action.Id, ToComments(await comments, null)));
    }

    /// <summary>Next page of comments.</summary>
    [EffectMethod(typeof(LoadMoreComments))]
    public async Task HandleMore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value is not { Id: { } id, Comments.Value: { HasMore: true } comments })
        {
            dispatcher.Dispatch(new PublicationCommandDone(nameof(LoadMoreComments), null));
            return;
        }

        dispatcher.Dispatch(new CommentsLoaded(id, ToComments(await api.ListCommentsAsync(id, comments.Page + 1), comments)));
    }

    /// <summary>Adds a comment.</summary>
    [EffectMethod]
    public async Task HandleAdd(AddComment action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        switch (await api.AddCommentAsync(id, action.Body))
        {
            case ApiResult<CommentDto>.Success { Value: var comment }:
                dispatcher.Dispatch(new CommentAdded(comment));
                dispatcher.Dispatch(new PublicationCommandDone(nameof(AddComment), null));
                await ReloadPublication(id, dispatcher);
                break;
            case ApiResult<CommentDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new PublicationCommandDone(nameof(AddComment), error));
                break;
        }
    }

    /// <summary>Removes an own comment and reloads the comments.</summary>
    [EffectMethod]
    public async Task HandleRemove(RemoveComment action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        if (await api.RemoveCommentAsync(id, action.CommentId) is ApiResult.Failure { Error: var error })
        {
            dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
            return;
        }

        dispatcher.Dispatch(new OpenPublication(id));
    }

    /// <summary>Sets the reaction; the counts are reloaded from the API.</summary>
    [EffectMethod]
    public async Task HandleReact(SetReaction action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        var result = await api.SetReactionAsync(id, action.Reaction);
        if (result is ApiResult.Failure { Error: var error })
        {
            dispatcher.Dispatch(new PublicationCommandDone(nameof(SetReaction), error));
            dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
            return;
        }

        await ReloadPublication(id, dispatcher);
        dispatcher.Dispatch(new PublicationCommandDone(nameof(SetReaction), null));
    }

    /// <summary>Loads the own ingredients for the matching dialog.</summary>
    [EffectMethod(typeof(PrepareCopy))]
    public async Task HandlePrepareCopy(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Dispatch(new CopyIngredientsLoaded(
            await api.ListIngredientsAsync() is ApiResult<PagedResult<IngredientDto>>.Success { Value: var page } ? page.Items : []));
    }

    /// <summary>Saves the recipe to the user's recipes.</summary>
    [EffectMethod]
    public async Task HandleCopy(CopyPublication action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        switch (await api.CopyAsync(id, action.Request))
        {
            case ApiResult<RecipeDto>.Success { Value: var recipe }:
                dispatcher.Dispatch(new PublicationCommandDone(nameof(CopyPublication), null));
                dispatcher.Dispatch(new ShowToast(Toast.Success($"{recipe.Title} saved to your recipes")));
                dispatcher.Dispatch(new RecipesChanged());
                dispatcher.Dispatch(new PublicationCopied(recipe.Id));
                break;
            case ApiResult<RecipeDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new PublicationCommandDone(nameof(CopyPublication), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
        }
    }

    /// <summary>Reports the publication or a comment.</summary>
    [EffectMethod]
    public async Task HandleReport(ReportContent action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        switch (await api.ReportAsync(id, new ReportRequest(action.Reason, action.CommentId)))
        {
            case ApiResult<ReportCreatedResponse>.Success:
                dispatcher.Dispatch(new PublicationCommandDone(nameof(ReportContent), null));
                dispatcher.Dispatch(new ContentReported());
                dispatcher.Dispatch(new ShowToast(Toast.Success("Thanks, a moderator will look at it")));
                break;
            case ApiResult<ReportCreatedResponse>.Failure { Error: var error }:
                dispatcher.Dispatch(new PublicationCommandDone(nameof(ReportContent), error));
                break;
        }
    }

    /// <summary>Unpublishes (author only).</summary>
    [EffectMethod(typeof(UnpublishPublication))]
    public async Task HandleUnpublish(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Id is not { } id)
        {
            return;
        }

        if (await api.UnpublishAsync(id) is ApiResult.Failure { Error: var error })
        {
            dispatcher.Dispatch(new PublicationCommandDone(nameof(UnpublishPublication), error));
            dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
            return;
        }

        dispatcher.Dispatch(new PublicationCommandDone(nameof(UnpublishPublication), null));
        dispatcher.Dispatch(new ShowToast(Toast.Info("Removed from the library")));
        dispatcher.Dispatch(new PublicationUnpublished(id));
    }

    private async Task ReloadPublication(Guid id, IDispatcher dispatcher)
    {
        if (await api.GetAsync(id) is ApiResult<PublishedRecipeDto>.Success { Value: var publication })
        {
            dispatcher.Dispatch(new PublicationLoaded(id, new Loadable<PublishedRecipeDto>.Loaded(publication)));
        }
    }

    private static Loadable<PagedList<CommentDto>> ToComments(ApiResult<PagedResult<CommentDto>> result, PagedList<CommentDto>? previous) => result switch
    {
        ApiResult<PagedResult<CommentDto>>.Success { Value: var page } =>
            new Loadable<PagedList<CommentDto>>.Loaded(previous is null ? PagedList.From(page) : previous.Append(page)),
        ApiResult<PagedResult<CommentDto>>.Failure { Error: var error } when previous is null => new Loadable<PagedList<CommentDto>>.Failed(error),
        _ => new Loadable<PagedList<CommentDto>>.Loaded(previous!),
    };
}
