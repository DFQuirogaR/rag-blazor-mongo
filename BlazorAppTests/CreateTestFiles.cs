using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;

class Program
{
    static void Main()
    {
        CreateTestWordDocument();
        Console.WriteLine("Test files created successfully!");
    }

    static void CreateTestWordDocument()
    {
        string filePath = "TestData/test-document.docx";
        
        // Ensure directory exists
        Directory.CreateDirectory("TestData");
        
        using (WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
        {
            // Add main document part
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            
            // Add core properties part
            CoreFilePropertiesPart coreProps = doc.AddCoreFilePropertiesPart();
            coreProps.Properties = new DocumentFormat.OpenXml.CustomProperties.Properties(
                new DocumentFormat.OpenXml.CustomProperties.HeaderTextProperties(
                    new DocumentFormat.OpenXml.CustomProperties.Title() { Val = "Test Document" },
                    new DocumentFormat.OpenXml.CustomProperties.Creator() { Val = "Test User" },
                    new DocumentFormat.OpenXml.CustomProperties.Created() { Val = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                )
            );
            
            Body body = mainPart.Document.Body;
            
            // Add a title (heading 1)
            AddHeading(body, "Test Document Title", 1);
            
            // Add a section with heading 2
            AddHeading(body, "Introduction", 2);
            AddParagraph(body, "This is a test paragraph to verify text extraction functionality.");
            
            // Add a list
            AddHeading(body, "Test List", 2);
            AddListParagraph(body, "First item in list");
            AddListParagraph(body, "Second item in list");
            AddListParagraph(body, "Third item in list");
            
            // Add a table
            AddHeading(body, "Sample Table", 2);
            AddTable(body);
            
            // Add more content
            AddHeading(body, "Conclusion", 2);
            AddParagraph(body, "This is the conclusion paragraph.");
            
            // Save the document
            mainPart.Document.Save();
        }
    }
    
    static void AddHeading(Body body, string text, int level)
    {
        var paragraph = new Paragraph();
        var paragraphProperties = new ParagraphProperties();
        
        // Apply heading style
        var paragraphStyleId = new ParagraphStyleId() { Val = $"Heading{level}" };
        paragraphProperties.Append(paragraphStyleId);
        
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        paragraph.Append(paragraphProperties);
        paragraph.Append(run);
        
        body.Append(paragraph);
    }
    
    static void AddParagraph(Body body, string text)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        paragraph.Append(run);
        body.Append(paragraph);
    }
    
    static void AddListParagraph(Body body, string text)
    {
        var paragraph = new Paragraph();
        
        // Add list property
        var paragraphProperties = new ParagraphProperties();
        var numPr = new NumberingProperties();
        var numberingId = new NumberingId() { Val = 1 };
        numPr.Append(numberingId);
        paragraphProperties.Append(numPr);
        
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        
        paragraph.Append(paragraphProperties);
        paragraph.Append(run);
        body.Append(paragraph);
    }
    
    static void AddTable(Body body)
    {
        // Create a simple 2x2 table
        var table = new Table();
        var tableProperties = new TableProperties(
            new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.Append(tableProperties);
        
        // Table row 1 (header)
        var tableRow1 = new TableRow();
        var tableCell1 = new TableCell(new Paragraph(new Run(new Text("Column 1"))));
        var tableCell2 = new TableCell(new Paragraph(new Run(new Text("Column 2"))));
        tableRow1.Append(tableCell1, tableCell2);
        
        // Table row 2
        var tableRow2 = new TableRow();
        var tableCell3 = new TableCell(new Paragraph(new Run(new Text("Value 1"))));
        var tableCell4 = new TableCell(new Paragraph(new Run(new Text("Value 2"))));
        tableRow2.Append(tableCell3, tableCell4);
        
        table.Append(tableRow1, tableRow2);
        body.Append(table);
    }
}
