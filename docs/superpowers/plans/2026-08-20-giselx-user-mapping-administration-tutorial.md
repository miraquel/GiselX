# GiselX User Mapping and Administration Tutorial Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a polished English PDF that teaches current GiselX Sellout, Stock, and Item mapping workflows and the complete Administrator role/user workflow.

**Architecture:** Treat the current workspace source as the authority for labels and behavior, including uncommitted administrator cross-company updates. Author the guide with ReportLab from a deterministic temporary generator, write the stable final PDF to `output/pdf/`, and verify both extracted content and rendered page images before delivery.

**Tech Stack:** Python 3.11+, ReportLab, pdfplumber, pypdf, Poppler (`pdfinfo` and `pdftoppm`), PowerShell

**Spec:** `docs/superpowers/specs/2026-08-14-giselx-user-mapping-administration-tutorial-design.md`

## Global Constraints

- Deliver one English PDF with no screenshots and no sensitive credentials.
- Use current GiselX menu, page, field, button, canonical-field, permission, and role labels.
- Use the exact role name `Administrator`; this name activates current cross-company behavior.
- Include the latest workspace behavior for Administrator visibility across Sales Transaction, Stock, and Service Level data and exports.
- Use neutral labeled examples only; do not claim that production records were created.
- Use A4 pages, ASCII hyphens, consistent headers/footers, and approximately 8-10 pages.
- Keep intermediates in `tmp/pdfs/` and the final artifact in `output/pdf/`.

---

### Task 1: Freeze the Current Tutorial Source of Truth

**Files:**
- Read: `docs/superpowers/specs/2026-08-14-giselx-user-mapping-administration-tutorial-design.md`
- Read: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`
- Read: `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml`
- Read: `GiselX.Web/Areas/Admin/Views/StockTemplateMappings/Edit.cshtml`
- Read: `GiselX.Web/Areas/Admin/Views/ItemMappings/Index.cshtml`
- Read: `GiselX.Web/Areas/Admin/Views/ItemMappings/Edit.cshtml`
- Read: `GiselX.Web/Areas/Admin/Pages/Roles/Create.cshtml`
- Read: `GiselX.Web/Areas/Admin/Pages/Roles/RolePermissions.cshtml`
- Read: `GiselX.Web/Areas/Admin/Pages/Users/Create.cshtml`
- Read: `GiselX.Web/Areas/Admin/Pages/Users/UserRoles.cshtml`
- Read: `GiselX.Common/Constants/PermissionConstants.cs`
- Read: `GiselX.Domain/SelloutCanonicalField.cs`
- Read: `GiselX.Service/SelloutCanonicalFieldCatalog.cs`
- Read: `GiselX.Domain/StockCanonicalField.cs`
- Read: `GiselX.Service/StockCanonicalFieldCatalog.cs`
- Read: current Administrator-related workspace diffs in `GiselX.Web/Program.cs`, `GiselX.Web/Views/SalesTransactions/Index.cshtml`, `GiselX.Web/Views/Stocks/Index.cshtml`, and `GiselX.Web/Views/ServiceLevels/Index.cshtml`
- Create: `tmp/pdfs/giselx-user-mapping-administration-tutorial-manuscript.md`

**Interfaces:**
- Consumes: approved tutorial specification and current workspace source.
- Produces: an exact, sectioned manuscript used by the PDF generator.

- [ ] **Step 1: Record the current workspace state**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' -c 'safe.directory=D:/D/Fauzi/Geisa code/Git/GiselX' status --short
& 'C:\Program Files\Git\cmd\git.exe' -c 'safe.directory=D:/D/Fauzi/Geisa code/Git/GiselX' diff --stat
```

Expected: user-owned changes remain untouched; the audit explicitly includes current uncommitted Administrator changes.

- [ ] **Step 2: Extract exact required canonical fields**

Record these Sellout required fields in the manuscript: `CustomerId`, `CustomerName`, `ProductId`, `ProductName`, `InvoiceNo`, `InvoiceDate`, `InvoiceQty`, and `InvoiceUnit`.

Record these Stock required fields: `ProductId`, `ProductName`, and `SaldoAkhir`.

- [ ] **Step 3: Record exact mapping and administration navigation**

Use these current routes and labels:

- **Admin > Companies > Edit Company**
- **Add Distributor Sellout Mapping** / **Manage Distributor Mappings**
- **Add Stock Mapping** / **Manage Stock Mappings**
- **Manage Item Mappings**
- **Admin > Roles > Create New Role**
- **Assign Permissions**
- **Admin > Users > Add New Record**
- **Assign Roles**

- [ ] **Step 4: Author the complete manuscript**

Write the following ordered sections with numbered steps, expected-result callouts, warnings, and troubleshooting:

1. Purpose and prerequisites
2. Mapping workflow overview
3. Open the correct company
4. Create a Sellout mapping
5. Create a Stock mapping
6. Create Item mappings manually
7. Import Item mappings in bulk
8. Enable Item identity mapping
9. Create the exact `Administrator` role
10. Assign permissions
11. Create a user
12. Assign the Administrator role
13. Latest Administrator cross-company behavior
14. Verification checklist
15. Troubleshooting and security notes

- [ ] **Step 5: Verify the manuscript has no placeholders or secrets**

Run:

```powershell
rg -n "TBD|TODO|PLACEHOLDER|Password=|ConnectionStrings|AdminSeed" tmp/pdfs/giselx-user-mapping-administration-tutorial-manuscript.md
```

Expected: no matches.

### Task 2: Build and Generate the PDF

**Files:**
- Read: `tmp/pdfs/giselx-user-mapping-administration-tutorial-manuscript.md`
- Create: `tmp/pdfs/generate_giselx_tutorial.py`
- Create: `output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf`

**Interfaces:**
- Consumes: the verified manuscript from Task 1.
- Produces: one deterministic A4 PDF with styled sections, tables, callouts, and page furniture.

- [ ] **Step 1: Mark the artifact operation immediately before authoring**

Run exactly once:

```powershell
node 'C:\Users\ahmad\.codex\plugins\cache\openai-primary-runtime\pdf\26.819.11345\skills\pdf\container_tools\mark_artifact_operation_started.mjs' --operation-kind create --expected-output-count 1 --output-format pdf
```

Expected: command exits successfully.

- [ ] **Step 2: Prepare the PDF runtime**

Run the following only for commands or modules that are missing:

```powershell
choco install python312 --yes
python -m pip install --target tmp/pdfs/python_packages reportlab pdfplumber pypdf
choco install poppler --yes
python -c "import sys; sys.path.insert(0, 'tmp/pdfs/python_packages'); import reportlab, pdfplumber, pypdf; print(reportlab.Version, pdfplumber.__version__, pypdf.__version__)"
pdfinfo -v
pdftoppm -v
```

Expected: Python imports all three modules, and both Poppler commands print their versions.

- [ ] **Step 3: Implement the deterministic generator**

Implement `tmp/pdfs/generate_giselx_tutorial.py` with these focused functions and concrete page settings:

```python
from pathlib import Path
import sys

sys.path.insert(0, str(Path("tmp/pdfs/python_packages").resolve()))

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, Frame, KeepTogether, PageTemplate, Paragraph,
    Spacer, Table, TableStyle,
)

NAVY = colors.HexColor("#17324D")
TEAL = colors.HexColor("#0D7C7B")
PALE_TEAL = colors.HexColor("#E8F5F4")
PALE_BLUE = colors.HexColor("#EDF3F8")
TEXT = colors.HexColor("#243442")


def build_styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "cover": ParagraphStyle("Cover", parent=base["Title"], fontName="Helvetica-Bold",
                                fontSize=28, leading=33, textColor=NAVY, alignment=TA_CENTER,
                                spaceAfter=12 * mm),
        "h1": ParagraphStyle("H1", parent=base["Heading1"], fontName="Helvetica-Bold",
                             fontSize=18, leading=22, textColor=NAVY, spaceBefore=5 * mm,
                             spaceAfter=3 * mm, keepWithNext=True),
        "h2": ParagraphStyle("H2", parent=base["Heading2"], fontName="Helvetica-Bold",
                             fontSize=13, leading=16, textColor=TEAL, spaceBefore=3 * mm,
                             spaceAfter=2 * mm, keepWithNext=True),
        "body": ParagraphStyle("Body", parent=base["BodyText"], fontName="Helvetica",
                               fontSize=10.2, leading=14.2, textColor=TEXT, spaceAfter=2.2 * mm),
        "step": ParagraphStyle("Step", parent=base["Heading3"], fontName="Helvetica-Bold",
                               fontSize=11, leading=14, textColor=NAVY, keepWithNext=True),
        "small": ParagraphStyle("Small", parent=base["BodyText"], fontName="Helvetica",
                                fontSize=8.5, leading=11, textColor=TEXT),
    }


def header_footer(canvas, document) -> None:
    canvas.saveState()
    if document.page > 1:
        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(colors.HexColor("#617386"))
        canvas.drawString(16 * mm, 10 * mm, "GiselX User Tutorial")
        canvas.drawRightString(A4[0] - 16 * mm, 10 * mm, f"Page {document.page}")
        canvas.setStrokeColor(colors.HexColor("#D8E2EA"))
        canvas.line(16 * mm, 14 * mm, A4[0] - 16 * mm, 14 * mm)
    canvas.restoreState()


def add_callout(story: list, title: str, body: str, kind: str,
                styles: dict[str, ParagraphStyle]) -> None:
    background = {"tip": PALE_TEAL, "warning": colors.HexColor("#FFF4DE")}.get(kind, PALE_BLUE)
    table = Table([[Paragraph(f"<b>{title}</b><br/>{body}", styles["body"])]],
                  colWidths=[A4[0] - 38 * mm])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), background),
        ("BOX", (0, 0), (-1, -1), 0.8, TEAL),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
    ]))
    story.extend([table, Spacer(1, 3 * mm)])


def add_step(story: list, number: int, title: str, paragraphs: list[str],
             styles: dict[str, ParagraphStyle]) -> None:
    content = [Paragraph(f"Step {number}. {title}", styles["step"])]
    content.extend(Paragraph(paragraph, styles["body"]) for paragraph in paragraphs)
    story.append(KeepTogether(content))


def format_inline(text: str) -> str:
    import html
    import re
    value = html.escape(text, quote=False)
    value = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", value)
    value = re.sub(r"`(.+?)`", r'<font name="Courier">\1</font>', value)
    return value


def populate_story_from_manuscript(story: list, manuscript: str,
                                   styles: dict[str, ParagraphStyle],
                                   step_writer, callout_writer) -> None:
    import re
    lines = manuscript.splitlines()
    index = 0
    while index < len(lines):
        line = lines[index].strip()
        if not line:
            story.append(Spacer(1, 1.5 * mm))
            index += 1
            continue
        if line.startswith("| "):
            table_lines = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                table_lines.append(lines[index].strip())
                index += 1
            rows = [[format_inline(cell.strip()) for cell in row.strip("|").split("|")]
                    for row in table_lines if not re.fullmatch(r"\|[\s|:-]+\|", row)]
            table_data = [[Paragraph(cell, styles["small"]) for cell in row] for row in rows]
            table = Table(table_data, repeatRows=1, hAlign="LEFT")
            table.setStyle(TableStyle([
                ("BACKGROUND", (0, 0), (-1, 0), NAVY),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#C8D5DF")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, PALE_BLUE]),
                ("LEFTPADDING", (0, 0), (-1, -1), 5),
                ("RIGHTPADDING", (0, 0), (-1, -1), 5),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]))
            story.extend([table, Spacer(1, 3 * mm)])
            continue
        step_match = re.match(r"^### Step (\d+):\s*(.+)$", line)
        if step_match:
            paragraphs = []
            index += 1
            while index < len(lines) and lines[index].strip() and not lines[index].startswith("#"):
                paragraphs.append(format_inline(lines[index].strip()))
                index += 1
            step_writer(story, int(step_match.group(1)), format_inline(step_match.group(2)),
                        paragraphs, styles)
            continue
        callout_match = re.match(r"^> (TIP|WARNING|EXPECTED):\s*(.+)$", line)
        if callout_match:
            kind = {"TIP": "tip", "WARNING": "warning", "EXPECTED": "expected"}[callout_match.group(1)]
            callout_writer(story, callout_match.group(1).title(),
                           format_inline(callout_match.group(2)), kind, styles)
        elif line.startswith("# "):
            story.append(Paragraph(format_inline(line[2:]), styles["cover"] if not story else styles["h1"]))
        elif line.startswith("## "):
            story.append(Paragraph(format_inline(line[3:]), styles["h1"]))
        elif line.startswith("### "):
            story.append(Paragraph(format_inline(line[4:]), styles["h2"]))
        elif line.startswith("- "):
            story.append(Paragraph(format_inline(line[2:]), styles["body"], bulletText="-"))
        else:
            story.append(Paragraph(format_inline(line), styles["body"]))
        index += 1


def build_tutorial(output_path: str) -> None:
    styles = build_styles()
    frame = Frame(16 * mm, 17 * mm, A4[0] - 32 * mm, A4[1] - 31 * mm,
                  leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    document = BaseDocTemplate(output_path, pagesize=A4, title="GiselX User Mapping and Administration Tutorial",
                               author="GiselX", leftMargin=16 * mm, rightMargin=16 * mm,
                               topMargin=14 * mm, bottomMargin=17 * mm)
    document.addPageTemplates([PageTemplate(id="Tutorial", frames=[frame], onPage=header_footer)])
    story: list = []
    manuscript = Path("tmp/pdfs/giselx-user-mapping-administration-tutorial-manuscript.md").read_text(encoding="utf-8")
    if "Administrator" not in manuscript or "SaldoAkhir" not in manuscript:
        raise ValueError("Verified manuscript content is incomplete")
    populate_story_from_manuscript(story, manuscript, styles, add_step, add_callout)
    document.build(story)


if __name__ == "__main__":
    Path("output/pdf").mkdir(parents=True, exist_ok=True)
    build_tutorial("output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf")
```

Use the manuscript markers shown in Task 1: `#`/`##`/`###` headings, `### Step N: Title`, `> TIP:`, `> WARNING:`, `> EXPECTED:`, dash bullets, ordinary paragraphs, and Markdown pipe tables.

- [ ] **Step 4: Include the current Administrator update**

The generated content must state that the exact `Administrator` role:

- Enables cross-company visibility on Sales Transaction, Stock, and Service Level pages.
- Adds distributor/company context to administrator tables.
- Adds distributor filtering to Stock.
- Shows distributor names in completed Stock upload choices.
- Adds a leading **Distributor** column to administrator downloads.
- Uses `All_Distributors` in administrator export filenames.
- Does not replace permission claims; the role must still receive the required permissions.

- [ ] **Step 5: Generate the final PDF**

Run the generator and confirm the stable output path:

```powershell
python tmp/pdfs/generate_giselx_tutorial.py
Get-Item output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf | Select-Object FullName,Length,LastWriteTime
```

Expected: the PDF exists and has non-zero size.

### Task 3: Verify Content and Visual Layout

**Files:**
- Read: `output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf`
- Create: `tmp/pdfs/rendered/giselx-tutorial-*.png`
- Modify if required: `tmp/pdfs/generate_giselx_tutorial.py`
- Regenerate if required: `output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf`

**Interfaces:**
- Consumes: generated PDF.
- Produces: verified final PDF and temporary QA renders.

- [ ] **Step 1: Inspect PDF metadata and page count**

Run:

```powershell
pdfinfo output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf
```

Expected: A4 page size, approximately 8-10 pages, no malformed-file errors.

- [ ] **Step 2: Extract and validate text**

Use pdfplumber or pypdf to assert that the extracted text contains all of these strings:

```text
Sellout Mapping
Stock Mapping
Item Mappings
Administrator
Assign Permissions to Role
Add User
Assign Roles to User
CustomerId
SaldoAkhir
All_Distributors
Final Checklist
```

Also assert that extracted text does not contain `TBD`, `TODO`, connection strings, or configured passwords.

- [ ] **Step 3: Render every page to PNG**

Run:

```powershell
New-Item -ItemType Directory -Force tmp/pdfs/rendered | Out-Null
pdftoppm -png -r 150 output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf tmp/pdfs/rendered/giselx-tutorial
```

Expected: one PNG per PDF page.

- [ ] **Step 4: Visually inspect every rendered page**

Check the cover, contents, all tables, callouts, page breaks, headers, footers, and final checklist. Reject the artifact for clipped text, overlaps, orphaned headings, broken tables, black squares, unreadable glyphs, or inconsistent page numbering.

- [ ] **Step 5: Correct and regenerate until clean**

If a defect is found, patch only the temporary generator, regenerate the PDF, rerun Steps 1-4, and retain only the latest verified PDF.

- [ ] **Step 6: Run final requirement verification**

Re-read the specification and check every acceptance criterion against fresh `pdfinfo`, text-extraction, and rendered-page evidence. Confirm exactly one final PDF exists in `output/pdf/` for this task.

- [ ] **Step 7: Commit only the tutorial artifacts owned by this task**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' -c 'safe.directory=D:/D/Fauzi/Geisa code/Git/GiselX' add -- docs/superpowers/specs/2026-08-14-giselx-user-mapping-administration-tutorial-design.md docs/superpowers/plans/2026-08-20-giselx-user-mapping-administration-tutorial.md output/pdf/GiselX_User_Mapping_and_Administration_Tutorial.pdf
& 'C:\Program Files\Git\cmd\git.exe' -c 'safe.directory=D:/D/Fauzi/Geisa code/Git/GiselX' commit -m 'docs: add GiselX mapping and administrator tutorial'
```

Expected: the commit contains only the amended spec, implementation plan, and final PDF; all pre-existing user changes remain untouched.
