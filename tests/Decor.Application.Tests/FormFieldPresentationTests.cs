using System.Xml.Linq;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public sealed class FormFieldPresentationTests
{
    [Theory]
    [InlineData("ProductsView.axaml", "ProductCodeDisplay")]
    [InlineData("CustomersView.axaml", "CodeDisplay")]
    [InlineData("SuppliersView.axaml", "CodeDisplay")]
    [InlineData("EmployeesView.axaml", "EmployeeCodeDisplay")]
    [InlineData("BrandsView.axaml", "BrandCodeDisplay", false)]
    [InlineData("ServicesView.axaml", "ServiceCodeDisplay")]
    [InlineData("UserFormView.axaml", "UserCodeDisplay")]
    [InlineData("ProductEditWindow.axaml", "ProductId")]
    [InlineData("EditUserWindow.axaml", "UserCodeDisplay")]
    public void Identity_has_one_raised_panel_without_individual_cards(string view, string codeBinding, bool hasState = true)
    {
        var document = Read(view);
        var code = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == $"{{Binding {codeBinding}}}");
        var panel = code.Ancestors().First(element => element.Name.LocalName == "Border");
        if (hasState)
        {
            var state = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ToggleSwitch");
            Assert.Same(panel, state.Ancestors().First(element => element.Name.LocalName == "Border"));
        }
        Assert.Equal("{DynamicResource DecorSurfaceRaisedBrush}", (string?)panel.Attribute("Background"));
        Assert.Equal("1", (string?)panel.Attribute("BorderThickness"));
        Assert.Equal("4", (string?)panel.Attribute("CornerRadius"));
        Assert.DoesNotContain(panel.Descendants(), element => element.Name.LocalName == "Border");
        Assert.DoesNotContain(panel.DescendantsAndSelf().Attributes(), attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired");
        Assert.Equal("{DynamicResource DecorFontWeightHeading}", (string?)code.Attribute("FontWeight"));
        Assert.All(panel.Descendants().Where(element => (string?)element.Attribute("Text") is "Código" or "Estado"),
            label => Assert.Equal("{DynamicResource DecorFontWeightHeading}", (string?)label.Attribute("FontWeight")));
    }

    [Fact]
    public void Product_marks_description_and_brand_but_not_optional_barcode()
    {
        var document = Read("ProductsView.axaml");
        AssertRequired(document, "Text", "{Binding Description}", true);
        AssertRequired(document, "SelectedItem", "{Binding SelectedBrand}", true);
        AssertRequired(document, "Text", "{Binding Barcode}", false);
    }

    [Theory]
    [InlineData("ProductsView.axaml", "Description,SelectedBrand,SelectedSubgroup")]
    [InlineData("ProductEditWindow.axaml", "Description,SelectedBrand,SelectedSubgroup")]
    [InlineData("CustomersView.axaml", "Name")]
    [InlineData("SuppliersView.axaml", "Name")]
    [InlineData("EmployeesView.axaml", "Name")]
    [InlineData("BrandsView.axaml", "BrandName")]
    [InlineData("ServicesView.axaml", "Description")]
    [InlineData("UserFormView.axaml", "Username")]
    [InlineData("CreateUserWindow.axaml", "Username")]
    [InlineData("EditUserWindow.axaml", "Username")]
    [InlineData("GroupsView.axaml", "Name")]
    [InlineData("RolesView.axaml", "")]
    [InlineData("LoginWindow.axaml", "UserIdInput,Password")]
    [InlineData("ChangePasswordWindow.axaml", "NewPassword,ConfirmPassword")]
    public void Required_markers_match_only_the_actual_editable_contract(string view, string expected)
    {
        var marked = Read(view).Descendants().Where(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "DecorRequiredField.IsRequired" && attribute.Value == "True")).ToArray();
        var expectedBindings = expected.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedBindings.Length, marked.Length);
        foreach (var binding in expectedBindings)
            Assert.Single(marked, element => element.Attributes().Any(attribute =>
                attribute.Value == $"{{Binding {binding}}}" || attribute.Value == $"{{Binding {binding}, Mode=TwoWay}}"));
        Assert.DoesNotContain(marked, element => (string?)element.Attribute("IsReadOnly") == "True"
            || element.Name.LocalName == "ToggleSwitch");
    }

    [Theory]
    [InlineData("ProductsView.axaml")]
    [InlineData("CustomersView.axaml")]
    [InlineData("SuppliersView.axaml")]
    [InlineData("EmployeesView.axaml")]
    [InlineData("BrandsView.axaml")]
    [InlineData("ServicesView.axaml")]
    [InlineData("UserFormView.axaml")]
    [InlineData("EditUserWindow.axaml")]
    [InlineData("CreateUserWindow.axaml")]
    [InlineData("ProductEditWindow.axaml")]
    public void Ordinary_input_labels_use_base_weight_and_section_titles_keep_emphasis(string view)
    {
        var document = Read(view);
        var labels = document.Descendants().Where(element => element.Name.LocalName == "TextBlock"
            && element.Parent!.Elements().Any(sibling => sibling.Name.LocalName is "TextBox" or "NumericUpDown" or "ListBox"
                || sibling.Descendants().Any(child => child.Name.LocalName == "ComboBox"))
            && element.Attribute("FontWeight") is not null
            && element.Attribute("FontSize") is null
            && (string?)element.Attribute("Text") != "Classificação Hierárquica");
        Assert.NotEmpty(labels);
        Assert.All(labels, label => Assert.Equal("{DynamicResource DecorFontWeightBase}", (string?)label.Attribute("FontWeight")));
        Assert.Contains(document.Descendants(), element => (string?)element.Attribute("FontSize") == "{DynamicResource DecorFontSizeTitle}");
    }

    [Fact]
    public void Current_password_is_required_only_in_the_existing_conditional_flow()
    {
        var document = Read("ChangePasswordWindow.axaml");
        var field = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == "{Binding CurrentPassword}");
        Assert.Equal("{Binding IsCurrentPasswordRequired}", field.Attributes().Single(attribute =>
            attribute.Name.LocalName == "DecorRequiredField.IsRequired").Value);
        Assert.Contains(field.Ancestors(), element => (string?)element.Attribute("IsVisible") == "{Binding IsCurrentPasswordRequired}");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Text") == "*");
        Assert.Single(document.Descendants(), element => (string?)element.Attribute("PointerPressed") == "PasswordRequirements_OnPointerPressed");
    }

    [Fact]
    public void Login_footer_shows_system_version_and_database_connectivity()
    {
        var document = Read("LoginWindow.axaml");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding SystemVersion, StringFormat='Versão {0}'}");
        var databaseIcon = Assert.Single(document.Descendants(), element => element.Name.LocalName == "PathIcon"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "DecorIcon.Id"
                && attribute.Value == "{x:Static icons:DecorIconId+Common.Database}"));
        var iconHost = databaseIcon.Parent!;
        Assert.Equal("18", (string?)iconHost.Attribute("Width"));
        Assert.Equal("18", (string?)iconHost.Attribute("Height"));
        Assert.Equal("{Binding DatabaseConnectionDescription}", (string?)iconHost.Attribute("ToolTip.Tip"));
        Assert.Contains(iconHost.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("IsVisible") == "{Binding IsDatabaseOnline}"
            && (string?)element.Attribute("Background") == "#218647");
        var offlineX = Assert.Single(iconHost.Descendants(), element => element.Name.LocalName == "PathIcon"
            && (string?)element.Attribute("IsVisible") == "{Binding !IsDatabaseOnline}");
        Assert.Equal("#C62828", (string?)offlineX.Attribute("Foreground"));
        var status = Assert.Single(document.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding DatabaseStatusText}");
        Assert.Same(iconHost.Parent, status.Parent!.Parent);
        Assert.True(iconHost.Parent!.Elements().ToList().IndexOf(iconHost) < iconHost.Parent.Elements().ToList().IndexOf(status.Parent));
    }

    [Fact]
    public void Login_heading_uses_title_scale_and_has_space_before_the_fields()
    {
        var heading = Assert.Single(Read("LoginWindow.axaml").Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Bem-vindo!");

        Assert.Equal("32", (string?)heading.Attribute("FontSize"));
        Assert.Equal("{DynamicResource DecorFontWeightHeading}", (string?)heading.Attribute("FontWeight"));
        Assert.Equal("0,0,0,24", (string?)heading.Attribute("Margin"));
        Assert.Equal("Center", (string?)heading.Attribute("HorizontalAlignment"));
        Assert.Equal("Center", (string?)heading.Attribute("TextAlignment"));
        Assert.Equal("Center", (string?)heading.Parent!.Attribute("VerticalAlignment"));
        var renderTransform = Assert.Single(heading.Elements(), element => element.Name.LocalName == "TextBlock.RenderTransform");
        var titleTransform = Assert.Single(renderTransform.Elements(), element => element.Name.LocalName == "TranslateTransform");
        Assert.Equal("-20", (string?)titleTransform.Attribute("Y"));
    }

    [Fact]
    public void Permission_sections_are_not_mandatory_registration_fields()
    {
        var document = Read("RolesView.axaml");
        foreach (var title in new[] { "Grupos de Permissões", "Módulos", "Contextos" })
        {
            var heading = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Text") == title);
            Assert.Equal("{DynamicResource DecorFontWeightHeading}", (string?)heading.Attribute("FontWeight"));
        }
    }

    [Fact]
    public void Dto_validators_require_names_but_allow_empty_optional_contact_and_salary_fields()
    {
        var customer = new CustomerDTO(0, "Cliente", null, null, null, null, false);
        var supplier = new SupplierDTO(0, "Fornecedor", null, null, null, false);
        var employee = new EmployeeDTO(0, "Funcionario", null, null, null, null, null, false, null);
        Assert.Empty(new CustomerDTOValidator().Validate(customer));
        Assert.NotEmpty(new CustomerDTOValidator().Validate(customer with { Name = null }));
        Assert.Empty(new SupplierDTOValidator().Validate(supplier));
        Assert.NotEmpty(new SupplierDTOValidator().Validate(supplier with { CorporateName = null }));
        Assert.Empty(new EmployeeDTOValidator().Validate(employee));
        Assert.NotEmpty(new EmployeeDTOValidator().Validate(employee with { Name = null }));
        var brand = new BrandDTO(0, "Marca");
        Assert.Empty(new BrandDTOValidator().Validate(brand));
        Assert.NotEmpty(new BrandDTOValidator().Validate(brand with { BrandName = null }));
        var service = new ServiceDTO(0, "Servico", false, null, null, null, null);
        Assert.Empty(new ServiceDTOValidator().Validate(service));
        Assert.NotEmpty(new ServiceDTOValidator().Validate(service with { Description = null }));
    }

    [Fact]
    public void Product_dto_requires_description_and_brand_but_accepts_empty_barcode()
    {
        var product = new ProductDTO(0, null, false, "Produto", 0, 1, "Marca", 1, null,
            null, null, null, null, null, null, null, null, null, null, 0, (int)ProductType.Good, null, null, null, null, 1);
        var validator = new ProductDTOValidator();
        Assert.Empty(validator.Validate(product));
        Assert.NotEmpty(validator.Validate(product with { Description = null }));
        Assert.NotEmpty(validator.Validate(product with { BrandID = null }));
    }

    [Fact]
    public void Quote_marks_only_outer_customer_and_employee_lookup_borders_as_required()
    {
        var document = Read("QuotesView.axaml");
        var markers = document.Descendants().Attributes().Where(attribute =>
            attribute.Name.LocalName == "DecorRequiredField.IsRequired").ToArray();
        Assert.Equal(2, markers.Length);
        foreach (var binding in new[] { "CustomerSummary", "EmployeeSummary" })
        {
            var summary = Assert.Single(document.Descendants(), element =>
                (string?)element.Attribute("Text") == $"{{Binding {binding}}}");
            var border = summary.Ancestors().First(element => element.Name.LocalName == "Border");
            Assert.Equal("36", (string?)border.Attribute("Height"));
            var marker = Assert.Single(markers, attribute => ReferenceEquals(attribute.Parent, border));
            Assert.Equal("True", marker.Value);
        }
    }

    private static void AssertRequired(XDocument document, string property, string binding, bool required)
    {
        var field = Assert.Single(document.Descendants(), element => (string?)element.Attribute(property) == binding);
        Assert.Equal(required, field.Attributes().Any(attribute => attribute.Name.LocalName == "DecorRequiredField.IsRequired" && attribute.Value == "True"));
    }

    private static XDocument Read(string view)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory!.FullName, "src", "Decor.AvaloniaUI", "Views", view));
    }
}