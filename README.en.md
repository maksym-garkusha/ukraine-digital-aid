# ukraine-digital-aid

[Українська версія](README.md)

Which donors fund digital transformation in Ukraine, through whom, how much and until when. Data from the international IATI registry, brought together on one searchable page for people who plan, fund or deliver digital projects in Ukrainian public bodies.

**Page: [maksym-garkusha.github.io/ukraine-digital-aid](https://maksym-garkusha.github.io/ukraine-digital-aid/)**. The data there is refreshed weekly.

This is an independent learning project built on IATI open data. It is not affiliated with any government body or donor and is not official statistics. The data is published by the donors themselves, so it is only as complete as they make it.

## Why

Most digital projects in Ukrainian public bodies are paid for by donors: the EU, the US, the UK, Germany, Sweden, Switzerland, UNDP, the World Bank. Each donor publishes its projects in the open [IATI](https://iatistandard.org/) standard, but there is no combined view of digital transformation in Ukraine as such: you have to go through the donors' sites one by one and work out each format.

The project takes every IATI activity with Ukraine as a recipient, picks out the ones about digital transformation and shows them in a single list: donor, implementers, amounts, dates, status. It also shows what is under way now, what ends within the next year and who implements it ("windows of opportunity"), what changed over the week (also as an Atom feed) and which records concern energy.

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

For the new sections of the page, as of the 18 November 2026 snapshot (689 selected records):

- **Energy.** 62 records carry the theme: 36 by a DAC sector from group 230 of at least 20%, and 26 by words alone, marked "worth checking". Only 14 of the 62 are direct. Of those, 8 qualify by sector, and they are 4 closed projects of the Swedish Radiation Safety Authority (Strålsäkerhetsmyndigheten), each published as two records: a registry of radiation sources, a dose registry, software licences and antivirus software for the IT department of the State Nuclear Regulatory Inspectorate of Ukraine (SNRIU). The other energy records are related: they mention digital work only in passing in the description.
- **Windows of opportunity.** 13 direct records in implementation end by 18 November 2027; 43 including related ones. None of the direct ones concerns energy; among the related ones 4 do: the World Bank's Second Power Transmission Project (implementer per the record: Ministry of Finance, 31 December 2026), FCDO's Green Cities, Infrastructure and Energy (PricewaterhouseCoopers, 31 December 2026), Sweden's NIR ITP Sustainability Impact Accelerator (31 December 2026) and BMZ's Strengthening of urban resilience in the Areas of Supply Infrastructure and Housing (implementer as the record names it: Ministerium für Entwicklung von Gemeinden und Territorien der Ukraine, 31 October 2027).
- **What changed.** The run cache holds no earlier snapshot yet, so the first comparison will appear after the weekly run on 23 November 2026.

The full report with donor and implementer tables and a list of what ends within a year: [docs/report.md](docs/report.md) (in Ukrainian). It is a snapshot as of the date at its top: in the repository the file is updated by hand with the `report` command. The weekly page build regenerates the report from fresh data and publishes it next to the page: [report.md](https://maksym-garkusha.github.io/ukraine-digital-aid/report.md).

## How "digital" records are selected

The selection is a heuristic. An activity is included if at least one of these holds:

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
- **30 random related records.** 7 are about digital transformation: Prozorro.Sale, software licences for nuclear plant safety analysis, telemedicine, the electronic regulatory platform of the communications regulator (NCEC), the VzayemoDiia e-participation platform, an online dialogue platform for labour migrants, and e-services of the tax service. The rest mention digital work only in passing, which is why related records are hidden by default.
- **30 rejected records from neighbouring sectors** (15110, 15111, 15113, 16010). None was clearly digital, 2 are open to question ("NGO Risk Scoring Center" and CoST reporting on disclosed data). 10 of the 30 cannot be judged: 6 have no description, and in 4 the donor withheld both title and description.

Two rules changed after the check. The list originally had a bare `connectivity`, but in Ukrainian records it is mostly about roads and logistics (Ukraine Transport Connectivity, RELINC), so the word now counts only as `internet connectivity`. Later, US Department of Defense records became related; two of the 60 checked direct records were such records, the M109A6 howitzer and "Project Sentinel Software". False positives among direct records were below a quarter even before these changes (6 of 60), so no second sample was drawn.

## Energy, weekly changes and windows of opportunity

**The energy theme** is given only to records already selected as digital. The reliable signal is a DAC sector from group 230 Energy: codes 231xx–236xx, covering policy, generation from renewable and non-renewable sources, nuclear energy and nuclear safety, transmission and distribution. If such sectors add up to at least 20% of a record, it gets the theme without reservation. Donors assign smaller shares to administrative records (USAID "Travel and Transportation" with 0.09% in 23110) and to multi-sector EU frameworks with 10% for everything, so a small share alone does not count. A record with a smaller share or no energy sector gets the theme if its title contains one of `energy`, `electricity`, `power grid`, `power plant`, `power transmission`, `hydropower`, `nuclear`, `renewable`, `solar`, `heating`, `natural gas`, `substation`, `Ukrenergo`, `Energoatom`, `Naftogaz` (in Ukrainian the stems `енерг`, `електроенерг`, `теплопостач`), or its description contains a phrase such as `energy sector`, `energy infrastructure`, `renewable energy`, `power supply`, `nuclear safety`. Such a record is marked "worth checking". Plain `energy` is not matched in descriptions: it appears in lists of sectors ("energy, transport, agri-food") and in one paragraph on energy security repeated across dozens of DG NEAR records about media, housing or statistics. With it, 70 records instead of 26 would be marked worth checking, most of them wrongly. Both lists live in [EnergyRules.cs](src/UkraineDigitalAid/Selection/EnergyRules.cs), and the page shows them in its methodology section.

**What changed this week.** Every `export` writes a short snapshot of the list (`snapshot.json`: identifier, status, end date, direct or related) and keeps a copy in the run cache, under `cache/snapshots`, for 35 days. The new snapshot is compared with the latest one that is at least 6 days older, so neither a mid-week rebuild after a push nor a scheduled run that starts a few hours late upsets the weekly comparison. If there is none, the oldest one from earlier days is used, and the page shows both dates. New records are those missing from the previous snapshot, which covers both freshly published records and older ones that now meet the selection rules. Ended means the status became Finalisation, Closed or Cancelled, or the end date passed between the snapshots. Records that left the list are only counted.

GitHub removes cache entries that have not been read for 7 days, and the weekly run sits right on that edge. So before `export` the workflow downloads the published `snapshot.json` from the page and passes it as `--previous`. It is used only if the cache has no snapshot for that date, and only with a date between 35 days ago and yesterday.

The same changes are published as Atom feeds: [changes.atom](https://maksym-garkusha.github.io/ukraine-digital-aid/changes.atom) in Ukrainian and [changes.en.atom](https://maksym-garkusha.github.io/ukraine-digital-aid/changes.en.atom) in English. A feed entry id depends only on the kind of change and the activity id, so a mid-week rebuild does not create duplicates in a reader.

**Windows of opportunity** are records with the status "Implementation" that end within 12 months of the snapshot date, soonest first, with implementers as the donor lists them. They are the same records the "in implementation, ending within 12 months" figure counts; a programme whose parts are listed separately is represented by its parts. The theme and the "include related" box above the table are shared with the list.

## What the data does not show

- The data is self-reported: each donor decides what to publish and when. Some update monthly, some once a year, and the status "Implementation" sometimes stays after the end date.
- `commitment` and `spend` refer to the entire activity, not just its digital part and not just Ukraine. Where Ukraine is one of several recipients or ICT is one of several sectors, the page shows those shares next to the amount and does not multiply anything. The donor table counts amounts only for records where Ukraine is 100% (178 of the 199 direct ones), and says so.
- A regional programme carries one amount for all countries. Ukraine's share is taken as the donor stated it. For 36 of the selected records d-portal shows a different share: it leaves out regions and in some records computes the share from transactions.
- Donors and funds that do not report to IATI are invisible here. Some donors (USAID and the US State Department among them) withhold titles, implementers or amounts under their own rules.
- The EU, Sweden and the UK publish a programme and its components separately. Totals count only the components; the list shows both records with a label.
- A project with a digital component that never names it with one of the keywords and does not report the ICT sector will not show up.
- The energy theme is set only on digital records. This is not a list of all donor support to the energy sector.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). No keys or database are needed.

```bash
git clone https://github.com/maksym-garkusha/ukraine-digital-aid.git
cd ukraine-digital-aid

dotnet run --project src/UkraineDigitalAid -- fetch                    # pull activities from d-portal into ./cache
dotnet run --project src/UkraineDigitalAid -- export site              # build site/data.json, snapshot.json and the change feeds
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
  Selection/    keyword list and selection rules, the energy theme
  Store/        on-disk cache in JSON Lines
  Stats/        summaries by donor, implementer, status, end year, windows of opportunity
  History/      snapshots of the list and comparison with the previous week
  Export/       data.json, snapshot.json and the Atom feeds for the page
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

**No server.** Once a week GitHub Actions pulls fresh records (the cache is kept between runs), builds `data.json` (about 850 KB, 180 KB compressed) and publishes it with the page on GitHub Pages. If d-portal is unavailable or the run collects less than half of last time's volume, the step fails and the previous page stays up.

Text from the data is always escaped before it reaches the page, and the CSP in a `<meta>` tag allows only the page's own script and style (by hash) and Google Fonts. After editing the script or style in `site/index.html` the hash has to be updated: the `PageCspTests` test fails and prints the new one, and the page is not published unless the tests pass.

**Provenance of the files.** `site.yml` signs a provenance attestation (SLSA provenance, via `actions/attest`) for everything the build generates for Pages: `data.json`, `snapshot.json`, `changes.atom`, `changes.en.atom` and `report.md`, exactly as they go to the site. The attestation records the repository, workflow, commit and run, and is signed by a short-lived Sigstore certificate issued for that run's OIDC token. Only a separate `attest` job holds the token and attestation write permissions; it builds nothing and makes no requests to d-portal.

Anyone can check it with the GitHub CLI (signed in to `gh`):

```bash
curl -fsSO https://maksym-garkusha.github.io/ukraine-digital-aid/data.json
gh attestation verify data.json -R maksym-garkusha/ukraine-digital-aid \
  --signer-workflow maksym-garkusha/ukraine-digital-aid/.github/workflows/site.yml --source-ref refs/heads/main
```

A file changed after the build fails the check, since the attestation is bound to the SHA-256 of its content. The attestation shows where a file came from, not that the code it was built from is right: it does not protect against a malicious commit on `main`.

Every job in `site.yml` and `tests.yml` starts with `step-security/harden-runner` in `audit` mode: it records where the runner connects and which processes it starts. That log will become the list of allowed endpoints, and the mode will then change to `block`.

Please report vulnerabilities privately, as described in [SECURITY.md](SECURITY.md).

## Data and licence

Data: [d-portal](https://d-portal.org/) on top of the [IATI](https://iatistandard.org/) registry. Records are published by the organisations themselves. Under the [IATI terms](https://web-terms.iatistandard.org/en/latest/copyright/), the data belongs to the organisation that published it and is released under the open licence listed for it on the IATI Registry. The d-portal code is open source under the MIT licence. `tests/.../Fixtures` holds shortened real records; their contact details and the name of an individual contractor have been replaced with made-up ones.

Code: MIT.
