# ukraine-digital-aid

[Українська версія](README.md)

Which donors fund digital transformation in Ukraine, through whom, how much and until when. Data from the international IATI registry, brought together on one searchable page.

**Page: [maksym-garkusha.github.io/ukraine-digital-aid](https://maksym-garkusha.github.io/ukraine-digital-aid/)**. The data there is refreshed weekly.

This is an independent learning project built on IATI open data. It is not affiliated with any government body or donor and is not official statistics. The data is published by the donors themselves, so it is only as complete as they make it.

## Why

Digital projects in Ukrainian public bodies are mostly paid for by donors: the EU, the US, the UK, Germany, Sweden, Switzerland, UNDP, the World Bank. Each donor publishes its projects in the open [IATI](https://iatistandard.org/) standard, but nobody offers a combined view of digital transformation in Ukraine specifically: you have to go through the donors' sites one by one and make sense of each format.

The project takes every IATI activity with Ukraine as a recipient, picks out the ones about digital transformation and shows them in a single list: donor, implementers, amounts, dates, status. It also shows what is in implementation now and what ends within the next year.

Who might find it useful:

- the teams of deputy ministers for digital development: which donors already work on related topics, whom to approach with a project, which programmes end soon;
- donors and implementing partners: to see each other and avoid duplicating work;
- IT companies: who commissions the work and who delivers it.

## What the data shows

d-portal snapshot of 17 November 2026.

- IATI holds 23,855 activities with Ukraine as a recipient. Under the rules below, 685 of them are digital: 227 direct and 458 related.
- Some donors publish a programme and its parts as separate records. Without that double counting there are 199 direct activities. 113 of them have the status "Implementation", and 13 of those 113 end by 17 November 2027.
- The direct activities come from 34 donors. The most records are published by the Swiss Agency for Development and Cooperation (SDC, 46), Sweden (22), the German Federal Foreign Office (20), the European Commission's DG NEAR (13) and WHO (13).

These numbers need some context:

- **SDC's 46 records are mostly one programme.** E-Governance for Accountability and Participation (EGAP) is published as separate records per year and per contract: the same contract number appears in `CH-4-2022-2013006824`, `CH-4-2023-2013006824`, `CH-4-2024-2013006824`. The page does not try to merge them, as that would mean guessing another organisation's identifier scheme.
- **Military assistance is among the related records.** The US Department of Defense published 27 of the selected records: cyber support to Ukraine's Ministry of Defence under USAI and software for military equipment provided under Presidential Drawdown Authority. 19 of them have a keyword in the title, but all 27 count as related (see the rule below), so they are not among the direct records or in the direct implementers table.
- **Status is not always current.** Of the 113 direct records marked "Implementation", 71 are past their end date, and 37 of those are SDC's yearly records. The page shows this number under the "Implementation" figure.
- **Amounts are not always there.** 206 records have no or zero commitments, 136 name no implementer at all, and in 22 the donor withheld the title of the activity itself.

The full report with donor and implementer tables and a list of what ends within a year: [docs/report.md](docs/report.md) (in Ukrainian). It is produced by the `report` command and rebuilt weekly together with the page.

## How "digital" records are selected

To be clear: this is a heuristic. An activity is included if at least one of these holds:

1. **Sector.** Its DAC sectors include 22040 (ICT), 22010 (communications policy) or 22020 (telecommunications). 22030 (radio, TV, print media) is left out: that is media.
2. **A keyword in the title** from the list below.
3. **A keyword only in the description**, not in the title. This is a weaker signal.

Records with an ICT sector share of at least 20% or a keyword in the title count as direct; the rest are related. One exception: records from the US Department of Defense (publisher `US-GOV-7`) are always related. They are military supplies, software for equipment and cyber support for the Ministry of Defence, and without the exception the words `software` and `cyber` in their titles would make them direct. A rule based on DAC sector group 152 (Conflict, peace and security) was tried and dropped: donors file civilian cybersecurity for government bodies (German Federal Foreign Office), SDC's e-governance work and digital evidence for the courts (EU) under the same sectors. The list of such publishers is in [DigitalRules.cs](src/UkraineDigitalAid/Selection/DigitalRules.cs).

The page shows direct records by default, and related ones appear when a box is ticked. Each record carries the reason it was selected and what exactly matched: a sector code with its share, or a word.

Keywords (case-insensitive, whole words only; a trailing "s" is allowed):

`digital`, `digitalisation`, `digitalization`, `digitisation`, `digitization`, `e-governance`, `e-government`, `egov`, `govtech`, `cyber`, `cybersecurity`, `ICT` / `ICTs`, `information system` / `information systems`, `information technology` / `information technologies`, `IT` (only as a separate word in capitals), `registry` / `registries`, `state register`, `electronic register`, `unified register`, `digital register` (a bare `register` does not count), `open data`, `interoperability`, `Diia`, `Trembita`, `eHealth`, `e-health`, `e-services`, `electronic services`, `software`, `data management`, `broadband`, `internet connectivity`, `artificial intelligence`, and for records in Ukrainian the stems `цифров`, `кібер`, `електронн` at the start of a word.

The list lives in a single file, [Keywords.cs](src/UkraineDigitalAid/Selection/Keywords.cs), which also feeds the candidate search on d-portal and the page.

Tricky cases covered by tests: `IT` is not found in `ITALY` or in the English "it"; `digital` is not found in `digitally` or in `EU4DigitalUA`; `digital` appearing only in an implementer's name ("Ministry of Digital Transformation") does not select a record; a bare `register` does not count.

### Manual check

After the first collection, 120 records were checked by hand.

- **60 random direct records.** 6 are not about digital transformation: a project for small farms where the donor gave 20% to the ICT sector; a WHO output on mental health policy with "information systems" in its title; the media project "Investigative journalism meets IT-Expertise"; "Ukraine Transport Connectivity", which is about roads; a US Department of Energy record whose title is the name of an accounting system while the description is about humanitarian response; software for the M109A6 howitzer. 2 more cannot be judged from the text: the donor withheld the title, or the title says only "Project Sentinel Software".
- **30 random related records.** 7 are about digital transformation: Prozorro.Sale, software licences for nuclear plant safety analysis, telemedicine, the electronic regulatory platform of the communications regulator (NCEC), the VzayemoDiia e-participation platform, an online dialogue platform for labour migrants, and e-services of the tax service. The rest mention digital things in passing, which is why related records are hidden by default.
- **30 rejected records from neighbouring sectors** (15110, 15111, 15113, 16010). None was clearly digital, 2 are open to question ("NGO Risk Scoring Center" and CoST reporting on disclosed data). 10 of the 30 cannot be judged: 6 have no description, and in 4 the donor withheld both title and description.

Two rules changed after the check. The list originally had a bare `connectivity`, but in Ukrainian records it is mostly about roads and logistics (Ukraine Transport Connectivity, RELINC), so the word now counts only as `internet connectivity`. Later, US Department of Defense records became related; two of the 60 checked direct records were such records, the M109A6 howitzer and "Project Sentinel Software". False positives among direct records were below a quarter even before these changes (6 of 60), so no second sample was drawn.

## What the data does not show

- **The data is self-reported.** Each donor decides what to publish and when. Some update monthly, some once a year, and the status "Implementation" sometimes stays after the end date.
- **Amounts cover the whole activity.** `commitment` and `spend` refer to the entire activity, not just its digital part and not just Ukraine. Where Ukraine is one of several recipients or ICT is one of several sectors, the page shows those shares next to the amount and does not multiply anything. The donor table counts amounts only for records where Ukraine is 100% (178 of the 199 direct ones), and says so.
- **Multi-country programmes.** A regional programme carries one amount for all countries. Ukraine's share is taken as the donor stated it. For 36 of the selected records d-portal shows a different share: it leaves out regions and in some records computes the share from transactions.
- **Not everyone publishes.** Donors and funds that do not report to IATI are invisible here. Some donors (USAID and the US State Department among them) withhold titles, implementers or amounts under their own rules.
- **Programmes and their parts.** The EU, Sweden and the UK publish a programme and its components separately. Totals count only the components; the list shows both records with a label.
- **A heuristic selection.** A project with a digital component that never names it with one of the keywords and does not report the ICT sector will not show up.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). No keys or databases.

```bash
git clone https://github.com/maksym-garkusha/ukraine-digital-aid.git
cd ukraine-digital-aid

dotnet run --project src/UkraineDigitalAid -- fetch                    # pull activities from d-portal into ./cache
dotnet run --project src/UkraineDigitalAid -- export site              # build site/data.json
dotnet run --project src/UkraineDigitalAid -- report docs/report.md    # build the report
cd site && python3 -m http.server                                      # page at http://localhost:8000
dotnet test                                                            # from the repository root
```

My first `fetch` took 4 min 55 s and downloaded 1,237 full records. A repeat run takes unchanged records from the cache and took 32 s. The cache lives in `cache` (or `--cache <dir>`); a record is downloaded again when its title, status, dates or amounts change on d-portal, or when it is older than 30 days.

## How it works

```
src/UkraineDigitalAid/
  Iati/         d-portal client (paging, pauses, retries), IATI codelists, candidate collection
  Activities/   activity model and parsing of the full IATI record
  Selection/    keyword list and selection rules
  Store/        on-disk cache in JSON Lines
  Stats/        summaries by donor, implementer, status, end year
  Export/       data.json for the page
  Report/       Markdown report
site/           the page: one HTML file, no build step, Ukrainian and English
tests/          xUnit, fixtures taken from real records of different donors
```

**Where the data comes from.** The official IATI Datastore requires a subscription key, so the project uses [d-portal](https://d-portal.org/), an open service on top of the same registry. Collection runs in three steps. First a short list of all 23,855 activities with Ukraine as a recipient (title, status, dates, amounts). Then candidates: activities with ICT sectors, d-portal's full-text search for each keyword, and keywords in the titles from the list. The last step is needed because d-portal's search (PostgreSQL with the `simple` configuration) matches only whole lowercase words: it knows nothing about stems or plurals, and `IT` would match the English "it". Full records are downloaded only for candidates, and the exact rules are applied locally. Requests are paced, at most three at a time, and retried on failure: d-portal is a volunteer-run service.

**IATI is not uniform.** Every donor records the same things differently, and the parser copes with that:

- titles and descriptions come in several languages, and some donors repeat the same title three times;
- the World Bank lists every DAC sector twice next to its own taxonomy, while USAID and some Swedish records give sectors only on transactions; shares are then computed from transaction amounts;
- countries may also be given only on transactions;
- instead of an implementer there is often just an organisation type ("Developing country-based NGO", code `XM-DAC-23000`) or "Uncoded". Such entries stay out of the implementer table;
- an organisation's `@ref` may hold an internal supplier number, "1" instead of USAID's code, or a code with an en dash. Implementers are therefore grouped by `@ref` only when it looks like an IATI identifier, and by name otherwise. Names are only trimmed and case-folded, with no "smart" merging of similar names.

**Personal data.** The `contact-info` block with names, addresses and phone numbers is not read at all, and a test checks that. E-mail addresses in descriptions are replaced with `[e-mail]`. Some donors list individuals (grantees or contractors) among implementers; an implementer name without an identifier that consists of two to four plain words and no word typical of an organisation is not stored. The check is rough and also hides some organisation names, which the page shows as "implementer name withheld". Titles and descriptions of activities are shown as the donor published them.

**A page without a server.** Once a week GitHub Actions pulls fresh records (the cache is kept between runs), builds `data.json` (about 850 KB, 180 KB compressed) and publishes it with the page on GitHub Pages. If d-portal is unavailable or the run collects less than half of last time's volume, the step fails and the previous page stays up.

Text from the data reaches the page only escaped, and the CSP in a `<meta>` tag allows only the page's own script and style (by hash) and Google Fonts. After editing the script or style in `site/index.html` the hash has to be updated: the `PageCspTests` test fails and prints the new one, and the page is not published unless the tests pass.

## Data and licence

Data: [d-portal](https://d-portal.org/) on top of the [IATI](https://iatistandard.org/) registry. Records are published by the organisations themselves. Under the [IATI terms](https://web-terms.iatistandard.org/en/latest/copyright/), the data belongs to the organisation that published it and is released under the open licence listed for it on the IATI Registry. The d-portal code is open source under the MIT licence. `tests/.../Fixtures` holds shortened real records; their contact details and the name of an individual contractor have been replaced with made-up ones.

Code: MIT.
