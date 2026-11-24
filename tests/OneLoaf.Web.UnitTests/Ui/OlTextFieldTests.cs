using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Web.UnitTests.Ui;

public sealed class TextFieldModel
{
    [Required(ErrorMessage = "Enter your e-mail.")]
    public string? Email { get; set; }
}

public sealed class OlTextFieldTests : BunitContext
{
    [Fact]
    public void Render_WithLabelHelpAndSuffix_LinksLabelAndDescribesInput()
    {
        var cut = Render<TextFieldHost>(p => p
            .Add(h => h.Label, "Oven power")
            .Add(h => h.Help, "From the type plate")
            .Add(h => h.Suffix, "kW"));

        var input = cut.Find("input");
        var label = cut.Find("label");
        Assert.Equal(input.Id, label.GetAttribute("for"));
        Assert.Equal("Oven power", label.TextContent);
        Assert.Contains("ol-input--suffixed", input.ClassList);
        Assert.Equal("kW", cut.Find(".ol-field__suffix").TextContent);
        Assert.Equal(cut.Find(".ol-field__help").Id, input.GetAttribute("aria-describedby"));
        Assert.Null(input.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void Change_UserTypes_UpdatesBoundModel()
    {
        var model = new TextFieldModel();
        var cut = Render<TextFieldHost>(p => p.Add(h => h.Model, model));

        cut.Find("input").Change("baker@example.com");

        Assert.Equal("baker@example.com", model.Email);
    }

    [Fact]
    public void Submit_InvalidValue_ShowsErrorWithAriaInvalidAndDescribedBy()
    {
        var cut = Render<TextFieldHost>(p => p.Add(h => h.Help, "We never share it"));

        cut.Find("form").Submit();

        var input = cut.Find("input");
        var error = cut.Find(".ol-field__error");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Contains("Enter your e-mail.", error.TextContent);
        Assert.Equal($"{cut.Find(".ol-field__help").Id} {error.Id}", input.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Change_AfterError_ClearsErrorState()
    {
        var cut = Render<TextFieldHost>();
        cut.Find("form").Submit();

        cut.Find("input").Change("baker@example.com");

        Assert.Empty(cut.FindAll(".ol-field__error"));
        Assert.Null(cut.Find("input").GetAttribute("aria-invalid"));
    }
}
