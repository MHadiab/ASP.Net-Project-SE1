# Test Matrix — سرویس دفترکل و موجودی (S2)

این ماتریس، سناریوهای مهم سرویس را به تست‌های واقعی داخل `tests/` وصل می‌کند. هر ردیف جدول «تست‌های انجام‌شده» دقیقاً یک متد تست دارد. سناریوهایی که هنوز تست ندارند، در بخش «پوشش‌نیافته‌ها» با دلیل و برنامه آمده‌اند.



## راهنما

- **Unit:** تست بدون دیتابیس.
- **Integration:** تست با PostgreSQL واقعی (Testcontainers یا `TEST_CONNECTION_STRING`).
- **نوع:** `Main` سناریوی اصلی، `Edge` موارد مرزی و خطا.

اجرای همهٔ تست‌ها و یک تست تنها:

```
dotnet test tests/Ledger.Tests
dotnet test tests/Ledger.Tests --filter "FullyQualifiedName~Concurrent_postings_cannot_spend_the_same_funds"
```

## تست‌های انجام‌شده (بک‌اند)

### Story 3 — Hold (`HoldServiceTests`)

| شناسه | سناریو | نوع | نتیجهٔ مورد انتظار | تست (`کلاس.متد`) | لایه |
| --- | --- | --- | --- | --- | --- |
| TM-01 | ساخت Hold با مبلغ معتبر | Main | Hold با وضعیت ACTIVE ساخته می‌شود، `HeldAmount` برابر 5000 و `Version` برابر 2 می‌شود و `BookBalance` تغییر نمی‌کند | `HoldServiceTests.CreateHold_WithValidAmount_ShouldCreateHold` | Integration |
| TM-02 | درخواست تکراری با همان `transfer_id` و همان مشخصات | Main | همان Hold برگردانده می‌شود، مبلغ دوباره بلوکه نمی‌شود و فقط یک Hold در دیتابیس هست | `HoldServiceTests.CreateHold_WithDuplicateRequest_ShouldReturnExistingHold` | Integration |
| TM-03 | مبلغ صفر یا منفی (دو Test Case: `0` و `-100`) | Edge | خطای `AppException` | `HoldServiceTests.CreateHold_WithInvalidAmount_ShouldThrowValidation` | Integration |
| TM-04 | حساب وجود ندارد | Edge | `AppException` (NOT_FOUND) | `HoldServiceTests.CreateHold_WithNonExistingAccount_ShouldThrowNotFound` | Integration |
| TM-05 | حساب مسدود است (`PostingAllowed = false`) | Edge | `AppException` (ACCOUNT_BLOCKED) | `HoldServiceTests.CreateHold_WithBlockedAccount_ShouldThrowAccountBlocked` | Integration |
| TM-06 | ارز درخواست با ارز حساب فرق دارد (USD) | Edge | `AppException` (CURRENCY_MISMATCH) | `HoldServiceTests.CreateHold_WithDifferentCurrency_ShouldThrowCurrencyMismatch` | Integration |
| TM-07 | موجودی قابل‌برداشت کمتر از مبلغ | Edge | `AppException` (INSUFFICIENT_FUNDS) و `HeldAmount` همان صفر می‌ماند | `HoldServiceTests.CreateHold_WithInsufficientFunds_ShouldThrowInsufficientFunds` | Integration |
| TM-08 | حساب هست ولی ردیف موجودی ندارد | Edge | `AppException` | `HoldServiceTests.CreateHold_WithoutAccountBalance_ShouldThrowInsufficientFunds` | Integration |
| TM-09 | همان `transfer_id` با مبلغ متفاوت | Edge | `AppException` (CONFLICT)؛ مبلغ بلوکه‌شده همان 5000 می‌ماند | `HoldServiceTests.CreateHold_WithDuplicateTransferIdAndDifferentAmount_ShouldThrowConflict` | Integration |

### Story 5 — ثبت سند و Capture (`PostingServiceTests`، `JournalEntryValidatorTests`)

| شناسه | سناریو | نوع | نتیجهٔ مورد انتظار | تست (`کلاس.متد`) | لایه |
| --- | --- | --- | --- | --- | --- |
| TM-10 | ثبت یک انتقال معتبر | Main | شمارهٔ سند، مجموع، `PostedAt` به UTC و `BusinessDate` به وقت تهران درست‌اند؛ `BalanceAfter` ردیف‌ها 70 و 30 است و جمع کل موجودی‌ها صفر | `PostingServiceTests.Posts_entry_and_updates_balances_and_line_history` | Integration |
| TM-11 | Capture در تراکنش فراخواننده (دو حالت: بدون Hold دیگر و با Hold دیگر ۲۰ریالی) | Main | موجودی دفتری 20، `HeldAmount` برابر مبلغ Holdهای دیگر، Hold تبدیل به CAPTURED و سند با دو ردیف ثبت می‌شود؛ پیش از Commit بیرون از تراکنش چیزی دیده نمی‌شود | `PostingServiceTests.Capture_posts_reserved_amount_and_preserves_other_holds_in_callers_transaction` | Integration |
| TM-12 | تراکنش از قبل باز است | Main | Commit با فراخواننده است و Rollback او همه‌چیز را برمی‌گرداند | `PostingServiceTests.Caller_owns_commit_when_a_transaction_is_already_open` | Integration |
| TM-13 | سند متوازن (سه حالت: 100000، 250000 و `long.MaxValue`) | Main | مجموع یک سمت سند برگردانده می‌شود | `JournalEntryValidatorTests.Balanced_entry_returns_total_of_one_side` | Unit |
| TM-14 | خرج پول بلوکه‌شده با ثبت مستقیم سند | Edge | `INSUFFICIENT_FUNDS`، موجودی‌ها و تعداد سندها تغییر نمی‌کنند | `PostingServiceTests.Reserved_funds_cannot_be_spent_by_direct_posting` | Integration |
| TM-15 | دو برداشت ۸۰ریالی هم‌زمان از موجودی 100 | Edge | فقط یکی موفق می‌شود؛ موجودی مبدأ 20، مقصد 80 و جمع کل صفر | `PostingServiceTests.Concurrent_postings_cannot_spend_the_same_funds` | Integration |
| TM-16 | شکست درج سند (شناسهٔ تکراری) | Edge | `CONFLICT` و به‌روزرسانی‌های قبلی موجودی برگردانده می‌شود | `PostingServiceTests.Journal_insert_failure_rolls_back_preceding_balance_updates` | Integration |
| TM-17 | شکست دیرهنگام وقتی تراکنش مال فراخواننده است | Edge | تغییر بیرونی فراخواننده هم Rollback می‌شود | `PostingServiceTests.Late_failure_also_rolls_back_the_callers_transaction` | Integration |
| TM-18 | یک حساب در چند ردیف سند | Edge | `BalanceAfter` جاری درست است (90، 5، 10)، `LineNo` درست است و `Version` فقط یک‌بار بالا می‌رود | `PostingServiceTests.Repeated_account_lines_keep_the_running_balance_and_one_version_increment` | Integration |
| TM-19 | موجودی‌های Track‌شده در EF بعد از ثبت | Edge | مقادیر Track‌شده تازه می‌شوند و `HasChanges()` برابر false است | `PostingServiceTests.Previously_tracked_balances_are_refreshed_after_posting` | Integration |
| TM-20 | شکست درج سند در میانهٔ Capture | Edge | رزرو برگردانده می‌شود (`HeldAmount` = 100)، Hold همچنان ACTIVE است و سند و موجودی مقصد دست‌نخورده‌اند | `PostingServiceTests.Capture_journal_failure_restores_reservation_and_tracked_hold_changes` | Integration |
| TM-21 | سند نامتوازن، مبلغ صفر یا منفی (سه Test Case) | Edge | `UNBALANCED_ENTRY` با وضعیت 400 | `JournalEntryValidatorTests.Invalid_amounts_or_totals_are_rejected` | Unit |
| TM-22 | ارز پشتیبانی‌نشده (USD) | Edge | `CURRENCY_MISMATCH` با وضعیت 400 | `JournalEntryValidatorTests.Unsupported_currency_is_rejected` | Unit |
| TM-23 | سرریز مجموع مبالغ | Edge | خطای برنامه (`UNBALANCED_ENTRY`، 400) به‌جای کرش | `JournalEntryValidatorTests.Overflow_is_rejected_as_an_application_error` | Unit |

### Story 7 (بخشی) — تاریخچهٔ سندها (`JournalEntriesServiceTests`)

| شناسه | سناریو | نوع | نتیجهٔ مورد انتظار | تست (`کلاس.متد`) | لایه |
| --- | --- | --- | --- | --- | --- |
| TM-24 | وضعیت «معکوس‌شده» هنگام فیلتر جهت، حتی وقتی ردیف سند معکوس فیلتر شده (دو Test Case: DEBIT و CREDIT) | Edge | سند اصلی `IsReversed = true` است، سند معکوس خودش `false` و نوعش `REVERSAL` | `JournalEntriesServiceTests.Reversal_state_is_current_even_when_reversing_line_is_filtered_out` | Integration |

## تست‌های انجام‌شده (فرانت‌اند)

| شناسه | سناریو | نوع | نتیجهٔ مورد انتظار | تست (`کلاس.متد`) | لایه |
| --- | --- | --- | --- | --- | --- |
| FE-01 | بک‌اند در دسترس است و کامپوننت اتصال را نمایش می‌دهد | Main | متن «Backend is available» و کد 200 دیده می‌شود | `ApiConnectionTestTests.ShouldDisplaySuccessWhenBackendIsAvailable` | Component (bUnit) |
| FE-02 | نمایش خطای Problem Details | Edge | عنوان، کد وضعیت 422، `ACCOUNT_BLOCKED` و `detail` نمایش داده می‌شوند | `ProblemDetailsViewTests.ShouldDisplayProblemDetailsCorrectly` | Component (bUnit) |

## پوشش‌نیافته‌ها (و دلیل هر کدام)

| شناسه | سناریو | دلیل پوشش‌نیافتن | برنامه |
| --- | --- | --- | --- |
| UN-01 | درخواست هم‌زمان Hold (مثلاً ۵۰ درخواست موازی روی یک حساب) | `HoldServiceTests` به یک دیتابیس مشترک (`TEST_CONNECTION_STRING`) وصل است و داده را دستی پاک می‌کند، پس اجرای موازی و ایزوله ممکن نیست. تست هم‌زمانی فقط برای `PostingService` (TM-15) نوشته شده است | `HoldServiceTests` به الگوی Testcontainers (مثل `PostingServiceTests`) منتقل شود و سپس تست هم‌زمانی Hold اضافه شود |
| UN-02 | بررسی کد خطا (`Code` و `Status`) در تست‌های Hold (TM-03 تا TM-09) | این تست‌ها فقط نوع `AppException` را بررسی می‌کنند و کد را نه؛ پس تغییر اشتباه کد خطا دیده نمی‌شود | مقدار برگشتی `Assert.ThrowsAsync` گرفته شود و `Code` و `Status` با OpenAPI تطبیق داده شود |
| UN-03 | تست API روی POST `/api/v1/holds`: مسیر موفق (201) و مطابقت Schema | تست‌های موجود سرویس‌ها را مستقیم صدا می‌زنند و HTTP، Content-Type و Schema را نمی‌بینند | تست با `WebApplicationFactory<Program>` و اعتبارسنجی پاسخ با `OpenApi_LedgerAndInventory.yaml` (بخش ۴ گزارش) |
| UN-04 | تست API روی POST `/api/v1/holds` با ورودی نامعتبر (400) و حساب مسدود (422) | همان دلیل UN-03، و چون خطاها باید `application/problem+json` با فیلد `code` باشند، فقط در لایهٔ HTTP قابل‌اثبات است | همان تست‌های بخش ۴ |
| UN-05 | GET `/api/v1/holds/{holdId}` (200 و 404) و `/health` و `/ready` | همان دلیل UN-03 | همان تست‌های بخش ۴ |
| UN-06 | هدر `Idempotency-Key` و خطای `IDEMPOTENCY_KEY_REUSED` | پارامتر `idempotencyKey` در `HoldService` گرفته می‌شود ولی هنوز استفاده نمی‌شود؛ تکرار فعلاً فقط با یکتایی `transfer_id` (TM-02 و TM-09) مدیریت می‌شود | بعد از پیاده‌سازی مکانیزم Idempotency (T0.4) تست‌ها اضافه شود |
| UN-07 | Release و انقضای خودکار Hold (US-4) | این Story در سهم این جلسه نیست و کد آن نوشته نشده است | در جلسهٔ بعد همراه پیاده‌سازی تست شود |
| UN-08 | Endpoint کامل Capture (HTTP) و Reversal (US-6) | سرویس ثبت سند و رفتار تراکنشی Capture پوشش داده شده (TM-11، TM-17 و TM-20)، ولی Endpointهای آن‌ها هنوز در این جلسه ساخته نشده‌اند | هر Endpoint با ساخته‌شدن، تست API خودش را می‌گیرد |
| UN-09 | صفحه‌بندی (cursor، limit)، بازهٔ زمانی و مرز نیمه‌شب تهران در صورت‌حساب (US-7) | تست موجود فقط فیلتر جهت و `IsReversed` را می‌سنجد. مرز نیمه‌شب به زمان قابل‌کنترل (Fake Clock) نیاز دارد که هنوز در سرویس‌ها تزریق نشده | `IClock` اضافه شود و سپس تست‌های مرز تاریخ و cursor نوشته شود |
| UN-10 | تراز آزمایشی (US-8) | این Story در سهم این جلسه نیست | جلسهٔ بعد |
| UN-11 | فرانت: خطای شبکه یا پاسخ غیر از 200 در `ApiConnectionTest` | `FakeHttpMessageHandler` فقط کد وضعیت برمی‌گرداند و نمی‌تواند قطع شبکه (`HttpRequestException`) را شبیه‌سازی کند | `FakeHttpMessageHandler` یک گزینهٔ پرتاب خطا بگیرد و دو تست (500 و قطع شبکه) اضافه شود |

<!-- TODO(عضو ۳): ردیف‌های تست‌های Story 1 (ساخت حساب و مجوز حساب) بعد از ادغام فایل تست‌هایش به جدول «تست‌های انجام‌شده» اضافه شود و عدد خلاصه به‌روز شود. -->
