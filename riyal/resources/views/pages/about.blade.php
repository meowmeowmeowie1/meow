@extends('layouts.app')

@section('content')
<div class="mx-auto max-w-3xl">
    @if ($locale === 'ar')
        <h1 class="text-3xl font-extrabold text-gray-900">من نحن</h1>
        <div class="article-body mt-6">
            <p>«ريال» مدوّنة مالية مستقلة تكتب للقارئ في الخليج والسعودية بلغة واضحة وعملية. هدفنا تبسيط القرارات المالية اليومية: فتح الحسابات، إدارة الراتب، الادخار، التمويل الإسلامي، ومكافأة نهاية الخدمة — بأرقام واقعية وسياق محلي.</p>
            <h2>لماذا تثق بنا؟</h2>
            <p>كل مقال يحمل اسم كاتب حقيقي، ويُراجع يدويًا قبل النشر، ويُحدَّث عند تغيّر الأنظمة أو الأسعار. لا ننشر محتوى عامًا منسوخًا، بل تجارب وإرشادات قابلة للتطبيق في السوق المحلي.</p>
            <h2>إفصاح</h2>
            <p>نستعين بالذكاء الاصطناعي في البحث والصياغة الأولية، ثم يُراجع المحتوى ويُحرّر بشريًا قبل النشر. المعلومات لأغراض تعليمية ولا تُعدّ استشارة مالية شخصية.</p>
        </div>
    @else
        <h1 class="text-3xl font-extrabold text-gray-900">About Riyal</h1>
        <div class="article-body mt-6">
            <p>Riyal is an independent personal-finance blog written for readers in the Gulf and Saudi Arabia, in plain, practical language. We simplify everyday money decisions — opening accounts, managing a salary, saving, Islamic finance and end-of-service benefits — with realistic numbers and local context.</p>
            <h2>Why trust us</h2>
            <p>Every article carries a named author, is reviewed by a human before publishing, and is updated when regulations or rates change. We don't publish generic, copied content — only guidance you can actually apply in the local market.</p>
            <h2>Disclosure</h2>
            <p>We use AI to assist with research and first drafts; all content is reviewed and edited by a human before publishing. Information is for educational purposes and is not personal financial advice.</p>
        </div>
    @endif
</div>
@endsection
