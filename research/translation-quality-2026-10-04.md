# Offline translation quality repair

## Superseding update

Version 1.0.1 subsequently repaired the reproduced email/link/path/inline-code corruption and added automatic English/Chinese direction. The historical identifier limitation below is superseded for recognized protected spans; general model semantic errors remain. See `integrity-and-auto-direction-2026-10-05.md` for current scope and evidence.

## Scope and changes

The authorized repair reuses the installed Argos 1.9 English/Chinese models, CTranslate2 4.6.0 CPU INT8 inference and SentencePiece. No package or model was added. Product changes are confined to `scripts/translation-worker.py`.

- Preserve English titles, initials and dotted abbreviations during sentence splitting. Attach closing quotation marks to the preceding sentence.
- Increase the sentence chunk budget from 120 to 256 tokens. Prefer clause punctuation and whole-word boundaries for longer sentences; preserve every input token in order.
- Strip decoder boundary whitespace after converting the model's literal SentencePiece markers to spaces.
- Retain beam size 4 and length penalty 0.2. Comparisons against penalties 0.6/1.0 and beam size 6 did not establish consistent semantic improvement. The upstream [Argos implementation](https://github.com/argosopentech/argos-translate/blob/master/argostranslate/translate.py) also uses penalty 0.2. Parameter meanings were checked against [CTranslate2's primary documentation](https://opennmt.net/CTranslate2/python/ctranslate2.Translator.html) and the installed runtime.

## Inspectable comparison

`tests/TranslationQualityComparison.py` contains 28 fixed cases: greetings, negation, file operations, dates/decimals, dotted abbreviations, battery instructions, technical errors, multiple sentences, prices, links/email, long conditional sentences and quotations, in both directions. Raw baseline and optimized outputs are retained under `A:\Agent\Codex\Translation\accuracy`.

| Source | Before | After | Observation |
| --- | --- | --- | --- |
| The U.S. team released version 2.5.1 yesterday. | 美国.团队昨天发布了2.5.1版本. | 美国队昨天发布了2.5.1版. | Acronym no longer creates a false sentence boundary. |
| Dr. Smith measured 3.14 volts at 10:30 a.m. on Monday. | 医生史密斯在上午10: 30测量出3.14伏特.周一的时候 | Smith医生星期一上午10: 30测出3.14伏特. | The day remains attached to its measurement. The proper name remains Latin in the new result. |
| Long Chinese maintenance instructions | Ends with a new assertion that inspection reports will be reviewed and supervised. | Retains the condition that nobody can restart until the report is reviewed and supervisors confirm the original configuration was not lost. | Avoiding the artificial 120-token cut improves this fixture's conditional meaning. |

All 28 optimized fixtures completed; numeric-presence and selected negation/abbreviation/conditional checks passed. These are an authored comparison set and targeted checks, **not a standardized accuracy score**, independent human evaluation or a guarantee against mistranslation. Larger decoder searches were measured on the same corpus; they were not selected based on fluency alone.

## Remaining model limitations

- The installed small models still produce awkward phrasing and terminology errors. Examples: `请勿关闭计算机` becomes `Do not close the computer`; `不含税` becomes `free of taxes`, which can change the intended meaning.
- One English fixture translates the local part of an email address. This repair does not promise exact preservation of technical identifiers, links or addresses. No output substitution or fixture-specific correction dictionary was introduced.
- Initials/abbreviations are handled conservatively. Ambiguous abbreviations at a real sentence boundary and extremely long unbroken tokens can still challenge the heuristic.
- OCR errors, line wrapping and general cross-sentence pronoun resolution were not comprehensively assessed.
- Improving the model's underlying vocabulary/domain knowledge requires a separate model comparison. No larger model download is implied by this repair.

## Regression evidence

- 16 sentence/token boundary checks pass, including no dropped/reordered tokens, a subword boundary reproduction and clause boundaries.
- Real bidirectional service gate passes 13/13.
- Real CPU inference still completes with Python network APIs denied; two 100-line fixtures retain line boundaries. The scoped audit does not intercept native sockets or disconnect the machine.
- The GUI verifier now restores a minimized production window before locating controls by geometry. A minimized-window reproduction had incorrectly selected the source label as status; the product translator was not failing.
- Portable ZIP CRC verification now reads the finalized archive after closing its writer; validating the still-open writer had produced a false CRC error.

Source: `A:\Projects\Translation`. Evidence: `A:\Agent\Codex\Translation\accuracy`, `verification`, `release`.
