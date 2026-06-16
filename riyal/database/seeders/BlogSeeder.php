<?php

namespace Database\Seeders;

use App\Models\Category;
use App\Models\Post;
use App\Models\Setting;
use App\Models\Tag;
use App\Models\User;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\Hash;
use Illuminate\Support\Str;

class BlogSeeder extends Seeder
{
    public function run(): void
    {
        $author = User::firstOrCreate(
            ['email' => 'editor@riyal.blog'],
            [
                'name' => 'Khalid Al-Rashed',
                'password' => Hash::make('password'),
                'role' => 'admin',
                'bio' => 'Personal-finance writer focused on the Gulf market.',
            ],
        );

        $this->seedSettings();
        $categories = $this->seedCategories();
        $this->seedPosts($author, $categories);
    }

    private function seedSettings(): void
    {
        $settings = [
            'site_name_ar' => 'ريال',
            'site_name_en' => 'Riyal',
            'contact_email' => 'hello@riyal.blog',
            // Placeholders — replace with real AdSense IDs once approved.
            'adsense_client_id' => '',
            'adsense_slot_in_article' => '',
            'adsense_slot_mid_article' => '',
            'adsense_slot_below_post' => '',
            'adsense_slot_sidebar' => '',
            'ads_txt_line' => '',
            'analytics_id' => '',
            'ai_disclosure_ar' => 'بُحث وكُتب بمساعدة الذكاء الاصطناعي وروجِع بشريًا من قبل فريق التحرير.',
            'ai_disclosure_en' => 'Researched and written with AI assistance and reviewed by our editorial team.',
        ];

        foreach ($settings as $key => $value) {
            Setting::firstOrCreate(['key' => $key], ['value' => $value]);
        }
    }

    /**
     * @return array<string, Category>  keyed by "{locale}:{key}"
     */
    private function seedCategories(): array
    {
        $groups = [
            'saving' => [
                'ar' => ['التوفير والميزانية', 'الادخار وبناء ميزانية تناسب الراتب الخليجي.'],
                'en' => ['Saving & Budgeting', 'Saving and building a budget that fits a Gulf salary.'],
            ],
            'loans' => [
                'ar' => ['التمويل والقروض', 'القروض الشخصية والبطاقات الائتمانية بوضوح.'],
                'en' => ['Loans & Financing', 'Personal loans and credit cards, explained clearly.'],
            ],
            'islamic' => [
                'ar' => ['التمويل الإسلامي', 'المرابحة والتورق والصكوك ببساطة.'],
                'en' => ['Islamic Finance', 'Murabaha, Tawarruq and Sukuk made simple.'],
            ],
            'work' => [
                'ar' => ['العمل والرواتب', 'الراتب ومكافأة نهاية الخدمة وحقوق الموظف.'],
                'en' => ['Work & Salary', 'Salary, end-of-service benefits and employee rights.'],
            ],
        ];

        $cats = [];
        foreach ($groups as $key => $locales) {
            $groupId = (string) Str::uuid();
            foreach ($locales as $locale => [$name, $desc]) {
                $cats["{$locale}:{$key}"] = Category::firstOrCreate(
                    ['locale' => $locale, 'slug' => Str::slug($name) ?: $key],
                    ['name' => $name, 'description' => $desc, 'translation_group_id' => $groupId],
                );
            }
        }

        return $cats;
    }

    private function seedPosts(User $author, array $cats): void
    {
        // Shared UUIDs for AR<->EN translation pairs (drive hreflang + switcher).
        $pairBudget = (string) Str::uuid();
        $pairIslamic = (string) Str::uuid();
        $pairEos = (string) Str::uuid();
        $pairLoan = (string) Str::uuid();

        $posts = $this->postDefinitions();
        $groupMap = [
            'ar-budget-503020' => $pairBudget, 'en-simple-budget' => $pairBudget,
            'ar-islamic-basics' => $pairIslamic, 'en-islamic-basics' => $pairIslamic,
            'ar-end-of-service' => $pairEos, 'en-end-of-service' => $pairEos,
            'ar-advance-vs-loan' => $pairLoan, 'en-loan-vs-advance' => $pairLoan,
        ];

        $day = 1;
        foreach ($posts as $key => $def) {
            Post::firstOrCreate(
                ['locale' => $def['locale'], 'slug' => $def['slug']],
                [
                    'translation_group_id' => $groupMap[$key] ?? null,
                    'title' => $def['title'],
                    'excerpt' => $def['excerpt'],
                    'body' => $def['body'],
                    'author_id' => $author->id,
                    'category_id' => $cats[$def['category']]->id,
                    'status' => 'published',
                    'is_featured' => $def['featured'] ?? false,
                    'reading_minutes' => max(1, (int) ceil(str_word_count(strip_tags($def['body'])) / 200)),
                    'published_at' => now()->subDays(30 - $day++),
                ],
            );
        }
    }

    /**
     * The 12 starter posts (6 AR + 6 EN) from the build brief. Bodies are
     * concise representative drafts in Markdown — replace with full, human-
     * edited articles before applying to AdSense.
     */
    private function postDefinitions(): array
    {
        return [
            // ---------------- Arabic ----------------
            'ar-savings-account' => [
                'locale' => 'ar', 'category' => 'ar:saving', 'featured' => true,
                'slug' => 'فتح-حساب-توفير-في-السعودية',
                'title' => 'كيف تفتح حساب توفير في السعودية وتختار البنك المناسب',
                'excerpt' => 'دليل عملي لفتح حساب توفير في السعودية، مع معايير اختيار البنك والرسوم التي يجب الانتباه لها.',
                'body' => "فتح حساب توفير هو أول خطوة عملية نحو بناء عادة الادخار. في السعودية أصبحت العملية رقمية بالكامل لدى معظم البنوك، وتستغرق دقائق عبر التطبيق.\n\n## المستندات المطلوبة\n\n- الهوية الوطنية أو الإقامة سارية المفعول.\n- رقم جوال مسجّل في «أبشر».\n- عنوان وطني موثّق.\n\n## كيف تختار البنك المناسب؟\n\nلا تنظر إلى اسم البنك فقط، بل قارن هذه النقاط:\n\n1. **الرسوم**: بعض الحسابات تفرض حدًا أدنى للرصيد أو رسوم تحويل.\n2. **العائد**: حسابات التوفير المتوافقة مع الشريعة تمنح أرباحًا متغيّرة.\n3. **سهولة التطبيق**: جودة التطبيق البنكي تفرق كثيرًا في الاستخدام اليومي.\n\n## نصيحة أخيرة\n\nخصّص حساب التوفير لهدف واضح (طوارئ، سفر، أو دفعة أولى) وفعّل التحويل التلقائي عند نزول الراتب حتى تدّخر قبل أن تنفق.",
            ],
            'ar-advance-vs-loan' => [
                'locale' => 'ar', 'category' => 'ar:loans',
                'slug' => 'السلفة-من-العمل-أم-القرض-الشخصي',
                'title' => 'الفرق بين السلفة من العمل والقرض الشخصي — أيهما أوفر؟',
                'excerpt' => 'مقارنة واقعية بين السلفة من جهة العمل والقرض الشخصي من البنك من حيث التكلفة والمرونة.',
                'body' => "عند الحاجة لسيولة عاجلة، يتردد كثيرون بين طلب **سلفة من العمل** أو **قرض شخصي** من البنك. لكل خيار تكلفته وشروطه.\n\n## السلفة من العمل\n\nغالبًا بدون فوائد، وتُخصم من الراتب على دفعات. ميزتها أنها أرخص فعليًا، لكن مبلغها محدود ويعتمد على سياسة الشركة.\n\n## القرض الشخصي\n\nيمنحك مبلغًا أكبر ومدة أطول، لكنه يحمل **هامش ربح** (معدل نسبة سنوي) يرفع التكلفة الإجمالية.\n\n## المقارنة السريعة\n\n| المعيار | السلفة | القرض |\n|---|---|---|\n| التكلفة | غالبًا صفر | معدل سنوي |\n| المبلغ | محدود | أكبر |\n| السرعة | فورية | أيام |\n\n## الخلاصة\n\nللمبالغ الصغيرة والاحتياج المؤقت، السلفة أوفر دائمًا. أما للمشاريع الكبيرة فقد يكون القرض ضروريًا — بشرط أن لا يتجاوز القسط ثلث الراتب.",
            ],
            'ar-budget-503020' => [
                'locale' => 'ar', 'category' => 'ar:saving',
                'slug' => 'ميزانية-الراتب-قاعدة-50-30-20',
                'title' => 'ميزانية الراتب: قاعدة 50/30/20 معدّلة للوضع السعودي',
                'excerpt' => 'كيف تطبّق قاعدة 50/30/20 على راتبك مع مراعاة الإيجار والالتزامات في السوق السعودي.',
                'body' => "قاعدة **50/30/20** من أبسط طرق تقسيم الراتب: 50% للأساسيات، 30% للكماليات، 20% للادخار وسداد الديون.\n\n## لماذا نحتاج لتعديلها محليًا؟\n\nالإيجار في المدن الكبرى قد يلتهم أكثر من 50% وحده. لذلك نقترح نسخة واقعية:\n\n- **55% أساسيات**: إيجار، فواتير، مواصلات، طعام.\n- **25% نمط حياة**: مطاعم، اشتراكات، ترفيه.\n- **20% مستقبلك**: ادخار طوارئ ثم استثمار.\n\n## خطوات التطبيق\n\n1. اكتب دخلك الصافي بعد التأمينات.\n2. صنّف مصاريف آخر شهرين.\n3. اضبط الفئة التي تجاوزت نسبتها.\n\nالهدف ليس الكمال، بل أن تعرف أين يذهب راتبك وأن تدفع لنفسك أولًا.",
            ],
            'ar-islamic-basics' => [
                'locale' => 'ar', 'category' => 'ar:islamic', 'featured' => true,
                'slug' => 'أساسيات-التمويل-الإسلامي-المرابحة-والتورق',
                'title' => 'أساسيات التمويل الإسلامي: المرابحة والتورق ببساطة',
                'excerpt' => 'شرح مبسّط للفرق بين المرابحة والتورق وكيف يعملان في التمويل البنكي المتوافق مع الشريعة.',
                'body' => "التمويل الإسلامي يقوم على مبدأ تجنّب الفائدة الربوية واستبدالها بمعاملات قائمة على بيع حقيقي. أشهر صيغتين هما **المرابحة** و**التورق**.\n\n## المرابحة\n\nيشتري البنك سلعة تريدها (سيارة مثلًا) ثم يبيعها لك بثمن مؤجّل أعلى يشمل ربحًا معلومًا متفقًا عليه مسبقًا.\n\n## التورق\n\nيُستخدم للحصول على **سيولة نقدية**: يشتري البنك سلعة ويبيعها لك بالأجل، ثم تبيعها أنت لطرف ثالث نقدًا فتحصل على المال.\n\n## نقاط يجب الانتباه لها\n\n- تأكد من **هامش الربح الفعلي** لا الاسم فقط.\n- اقرأ شروط السداد المبكر.\n- قارن العرض مع أكثر من جهة.\n\nالتوافق مع الشريعة لا يعني بالضرورة الأرخص؛ المقارنة تبقى ضرورية.",
            ],
            'ar-end-of-service' => [
                'locale' => 'ar', 'category' => 'ar:work',
                'slug' => 'مكافأة-نهاية-الخدمة-كيف-تحسب',
                'title' => 'مكافأة نهاية الخدمة: كيف تُحسب وماذا تفعل بها',
                'excerpt' => 'طريقة احتساب مكافأة نهاية الخدمة في نظام العمل السعودي مع نصائح لإدارة المبلغ.',
                'body' => "مكافأة نهاية الخدمة حق نظامي للموظف عند انتهاء العلاقة العمالية، ويعتمد حسابها على **آخر راتب** و**سنوات الخدمة**.\n\n## طريقة الحساب\n\nوفق نظام العمل السعودي:\n\n- نصف شهر عن كل سنة من **الخمس سنوات الأولى**.\n- شهر كامل عن كل سنة بعد ذلك.\n\nوتُحسب السنوات الجزئية بالتناسب.\n\n## مثال مبسّط\n\nموظف راتبه 8000 ريال خدم 7 سنوات: (5 × نصف شهر) + (2 × شهر) = 2.5 + 2 = 4.5 شهر ≈ 36,000 ريال.\n\n## ماذا تفعل بالمبلغ؟\n\n1. خصّص صندوق طوارئ يغطي 3–6 أشهر.\n2. سدّد أي ديون مكلفة.\n3. استثمر الباقي بدل تركه في الحساب الجاري.",
            ],
            'ar-credit-card-mistakes' => [
                'locale' => 'ar', 'category' => 'ar:loans',
                'slug' => 'أخطاء-شائعة-في-بطاقة-الائتمان',
                'title' => 'أخطاء شائعة في استخدام بطاقة الائتمان وكيف تتجنبها',
                'excerpt' => 'أبرز الأخطاء التي ترفع تكلفة بطاقتك الائتمانية وطرق عملية لتفاديها.',
                'body' => "بطاقة الائتمان أداة مفيدة إن استُخدمت بذكاء، وعبء إن أُسيء استخدامها. إليك أكثر الأخطاء شيوعًا.\n\n## 1. سداد الحد الأدنى فقط\n\nهذا يبقي الرصيد ويراكم هامش الربح. القاعدة: **سدّد كامل المبلغ** قبل تاريخ الاستحقاق.\n\n## 2. السحب النقدي من البطاقة\n\nيحمل رسومًا فورية وهامشًا أعلى من المشتريات. تجنّبه قدر الإمكان.\n\n## 3. تجاوز 30% من الحد\n\nاستخدام نسبة عالية من حدّك الائتماني قد يؤثر على تقييمك الائتماني في «سمة».\n\n## 4. إهمال تاريخ الاستحقاق\n\nفعّل تنبيهًا أو السداد التلقائي حتى لا تدفع رسوم تأخير.\n\nالبطاقة وسيلة دفع، لا مصدر دخل إضافي — تعامل معها على هذا الأساس.",
            ],

            // ---------------- English ----------------
            'en-expat-account' => [
                'locale' => 'en', 'category' => 'en:saving', 'featured' => true,
                'slug' => 'opening-bank-account-saudi-arabia-expat',
                'title' => 'Opening a bank account in Saudi Arabia as an expat: full guide',
                'excerpt' => 'A step-by-step guide for expats opening their first bank account in Saudi Arabia, including documents and tips.',
                'body' => "Opening a bank account is one of the first things to sort out after arriving in Saudi Arabia. The process is mostly digital, but expats need a few specific documents.\n\n## What you'll need\n\n- A valid **Iqama** (residence permit).\n- A registered mobile number linked to Absher.\n- A national address.\n- Sometimes a salary certificate from your employer.\n\n## Choosing a bank\n\nDon't pick by brand alone. Compare:\n\n1. **Minimum balance** and monthly fees.\n2. **App quality** — you'll use it daily.\n3. **International transfer** costs if you send money home.\n\n## Tips\n\nMany banks let you open an account fully through the app once your Iqama is active. Keep your address in Absher up to date, since the account verification depends on it.",
            ],
            'en-end-of-service' => [
                'locale' => 'en', 'category' => 'en:work',
                'slug' => 'end-of-service-benefits-saudi-arabia',
                'title' => 'End-of-service benefits in Saudi Arabia: how they’re calculated',
                'excerpt' => 'Understand how your end-of-service gratuity is calculated under Saudi labour law, with a worked example.',
                'body' => "End-of-service benefit (gratuity) is a legal right when your employment ends. It is based on your **last wage** and **years of service**.\n\n## The formula\n\nUnder Saudi labour law:\n\n- **Half a month** per year for the first five years.\n- **One full month** per year after that.\n\nPartial years are paid pro-rata.\n\n## Worked example\n\nAn employee earning SAR 8,000 with 7 years of service: (5 × ½ month) + (2 × 1 month) = 2.5 + 2 = 4.5 months ≈ SAR 36,000.\n\n## What affects the amount\n\nResignation versus termination can change your entitlement, especially in the first years of service. Always check your contract and the latest regulation before you leave.",
            ],
            'en-remittances' => [
                'locale' => 'en', 'category' => 'en:saving',
                'slug' => 'sending-money-home-from-the-gulf',
                'title' => 'Sending money home from the Gulf: comparing remittance options',
                'excerpt' => 'Bank transfers, exchange houses and apps compared on cost, speed and exchange rate.',
                'body' => "Remittances are a monthly reality for millions of Gulf workers. The cheapest option depends on the destination, amount and how fast you need it to arrive.\n\n## Your main options\n\n1. **Exchange houses** — competitive rates, wide branch networks.\n2. **Bank transfers** — convenient but often a weaker exchange rate.\n3. **Apps** — fast and cheap for supported corridors.\n\n## What really costs you\n\nThe headline fee is only part of the story. The **exchange rate margin** often costs more than the visible fee. Always compare the *amount received* in the home currency, not just the fee.\n\n## Practical tip\n\nSend larger amounts less often to reduce per-transfer fees, and watch for promotional zero-fee days many providers offer to new users.",
            ],
            'en-simple-budget' => [
                'locale' => 'en', 'category' => 'en:saving',
                'slug' => 'simple-budget-that-works-on-a-gulf-salary',
                'title' => 'A simple budget that works on a Gulf salary',
                'excerpt' => 'A realistic budgeting framework adapted for high-rent Gulf cities and tax-free income.',
                'body' => "A budget on a Gulf salary has two quirks: **no income tax**, but often **high rent paid yearly**. A simple framework still works if you adapt it.\n\n## The adapted split\n\n- **55% essentials**: rent, bills, transport, groceries.\n- **25% lifestyle**: dining, subscriptions, travel.\n- **20% future you**: emergency fund, then investing.\n\n## Handle yearly rent\n\nDivide your annual rent by 12 and move that amount into a separate account each month, so the renewal never wipes out your savings.\n\n## Make it automatic\n\nThe moment your salary lands, auto-transfer your savings. Pay yourself first — what's left is what you actually have to spend.",
            ],
            'en-islamic-basics' => [
                'locale' => 'en', 'category' => 'en:islamic',
                'slug' => 'islamic-finance-basics-for-beginners',
                'title' => 'Islamic finance basics for beginners (Murabaha, Ijara, Sukuk)',
                'excerpt' => 'A plain-English introduction to the core Islamic finance structures you’ll meet at any Gulf bank.',
                'body' => "Islamic finance replaces interest (riba) with structures based on real trade, leasing or partnership. Here are the three you'll meet most often.\n\n## Murabaha (cost-plus sale)\n\nThe bank buys an asset you want and sells it to you for a known, agreed profit, paid in instalments.\n\n## Ijara (leasing)\n\nThe bank owns an asset and leases it to you; at the end you may own it. It's the Islamic equivalent of a lease-to-own.\n\n## Sukuk (Islamic bonds)\n\nInstead of lending at interest, sukuk represent **ownership** in an asset or project, and returns come from that asset's income.\n\n## Before you sign\n\nSharia-compliant doesn't automatically mean cheapest. Compare the effective profit rate across providers just as you would an interest rate.",
            ],
            'en-loan-vs-advance' => [
                'locale' => 'en', 'category' => 'en:loans',
                'slug' => 'personal-loan-vs-salary-advance-saudi-arabia',
                'title' => 'Personal loan vs. salary advance in Saudi Arabia: which costs less',
                'excerpt' => 'When a salary advance beats a personal loan — and when it doesn’t — for short-term cash needs.',
                'body' => "Need cash quickly? The choice is often between a **salary advance** from your employer and a **personal loan** from a bank.\n\n## Salary advance\n\nUsually interest-free and deducted from upcoming pay. Cheapest option, but the amount is limited and depends on company policy.\n\n## Personal loan\n\nLarger amount, longer term, but carries a **profit/interest rate** that raises the total cost.\n\n## Quick comparison\n\n| Factor | Advance | Loan |\n|---|---|---|\n| Cost | Often zero | Annual rate |\n| Amount | Limited | Larger |\n| Speed | Immediate | Days |\n\n## Bottom line\n\nFor small, short-term needs, the advance almost always wins. For big expenses a loan may be necessary — just keep the monthly instalment under a third of your salary.",
            ],
        ];
    }
}
