# BUG_FIXED_SESSION06

- سرویس و اعضای تیم: S2-LDG (سرویس دفترکل و موجودی، #C) | محمد مهدی قربی، محمد امین باغبان، هادی ابکار
- مخزن تمرین: https://github.com/AUT-SE1/LabBank-S2-Session06 (نسخه‌ی اصلاح‌شده در Fork: https://github.com/mmd-ghorbi/LabBank-S2-Session06)

## رفتار اشتباه و علت اصلی

**رفتار اشتباه:** در `GET /api/v1/accounts/{id}/entries` فیلتر `from` باید **شامل** (inclusive) باشد؛ README و قرارداد OpenAPI هر دو همین را می‌گویند. اما ردیفی که `posted_at` آن دقیقاً برابر `from` است در پاسخ نمی‌آمد.

**بازتولید (حساب دمو `0192a7d0-0000-7000-8000-000000000001`، `direction=DEBIT`):**

| ورودی | نتیجه‌ی مورد انتظار | نتیجه‌ی واقعی (نسخه‌ی باگ‌دار) |
|---|---|---|
| بدون `from` | دو ردیف (150000 و 100000) | دو ردیف |
| `from=2026-11-15T14:14:46.851941Z` (دقیقاً برابر `posted_at` ردیف 100000) | دو ردیف | **فقط** ردیف 150000 |
| `from=2026-11-15T14:14:46.851940Z` (یک میکروثانیه کمتر) | دو ردیف | دو ردیف |

**علت اصلی:** در `Services/JournalEntriesService.cs` (حدود خط ۱۵۷) شرط فیلتر `from` با عملگر `>` نوشته شده بود به‌جای `>=`:

```csharp
line => line.PostedAt > requestedFrom   // باگ: کران پایین exclusive می‌شود
```

## اصلاح انجام شده

حداقل تغییر: فقط یک عملگر.

```csharp
line => line.PostedAt >= requestedFrom
```

**پیش‌نیاز محیطی (جدا از باگ اصلی):** فایل `Migrations/20261030185603_AddLedgerAccountsAndBalances.Designer.cs` دو Attribute زیر را نداشت، پس EF آن Migration را نمی‌شناخت و روی دیتابیس خالی جدول‌های `ledger_accounts` و `account_balances` ساخته نمی‌شد (خطای `relation "ledger_accounts" does not exist`). این مورد در Commit جدا اصلاح شد:

```csharp
[DbContext(typeof(AppDbContext))]
[Migration("20261030185603_AddLedgerAccountsAndBalances")]
```
(همین مشکل روی `main` مخزن اصلی تیم هم وجود دارد و جداگانه پیگیری می‌شود.)

## تست ها و نتیجه اجرا

تست جدید: `JournalEntriesServiceTests.From_filter_includes_lines_posted_exactly_at_from`. این تست دو سند ثبت می‌کند، زمان دقیق ردیف بدهکار حساب مبدأ را از دیتابیس می‌خواند، آن را به‌عنوان `from` می‌دهد و انتظار دارد همان ردیف برگردد.

- تست پیش از اصلاح: **شکست**
  ```
  Assert.Single() Failure: The collection was empty
  at JournalEntriesServiceTests.cs(135)
  Test summary: total: 1, failed: 1, succeeded: 0
  ```
- تست پس از اصلاح: **موفق**
  ```
  Test summary: total: 1, failed: 0, succeeded: 1
  ```
- نتیجه کل تست ها: **۳۴ از ۳۴ موفق**
  ```
  Test summary: total: 34, failed: 0, succeeded: 34, skipped: 0
  ```
  نکته‌ی محیطی: در اولین اجرای کل تست‌ها ۸ تست `HoldServiceTests` با خطای `28P01: password authentication failed for user "labuser"` شکست خوردند، چون این کلاس به دیتابیس `labdb_test` با کاربر `labuser` (طبق `TEST_CONNECTION_STRING`) وصل می‌شود و این کاربر و دیتابیس روی سیستم ما ساخته نشده بود. این مشکل ربطی به باگ نداشت (احراز هویت پیش از رسیدن به کد سرویس شکست می‌خورد). پس از ساخت کاربر و دیتابیس تست و اعمال Migrationها روی آن، هر ۳۴ تست موفق شدند. همچنین به‌دلیل خطای ۴۰۳ Docker Hub، برای اجرای تست‌ها `TESTCONTAINERS_RYUK_DISABLED=true` تنظیم شد.

## فایل های تغییر یافته

- `src/Ledger_and_Inventory_Service/Services/JournalEntriesService.cs` — اصلاح `>` به `>=` (یک خط)
- `tests/Ledger.Tests/JournalEntriesServiceTests.cs` — افزودن تست بازتولید و `using System.Globalization;`
- `src/Ledger_and_Inventory_Service/Migrations/20261030185603_AddLedgerAccountsAndBalances.Designer.cs` — بازگرداندن دو Attribute (Commit جدا، پیش‌نیاز محیطی)

Commitها:
- `295eee8` fix(migrations): restore Migration attributes on AddLedgerAccountsAndBalances
- `e545742` fix(journal): make from filter inclusive

## نتیجه نهایي

باگ بازتولید، علتش (`>` به‌جای `>=` در کران پایین `from`) پیدا و با حداقل تغییر اصلاح شد. تست بازتولید پیش از اصلاح شکست می‌خورد و پس از آن موفق است، و کل مجموعه‌ی ۳۴ تستی بدون Regression سبز است. کد اصلاح‌شده فقط در مخزن تمرین (Fork) مانده و به مخزن اصلی تیم منتقل نشده است.
