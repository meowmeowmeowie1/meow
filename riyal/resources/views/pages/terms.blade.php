@extends('layouts.app')

@section('content')
<div class="mx-auto max-w-3xl">
    @if ($locale === 'ar')
        <h1 class="text-3xl font-extrabold text-gray-900">شروط الاستخدام</h1>
        <div class="article-body mt-6">
            <p>باستخدامك لهذا الموقع فإنك توافق على الشروط التالية.</p>
            <h2>طبيعة المحتوى</h2>
            <p>المحتوى المنشور لأغراض تعليمية وتثقيفية عامة، ولا يُعدّ استشارة مالية أو قانونية أو ضريبية شخصية. ننصح بمراجعة مختص قبل اتخاذ قرارات مالية.</p>
            <h2>الملكية الفكرية</h2>
            <p>جميع المقالات والتصاميم مملوكة لنا ولا يجوز إعادة نشرها دون إذن.</p>
            <h2>إخلاء المسؤولية</h2>
            <p>نبذل جهدنا لدقّة المعلومات وتحديثها، لكننا لا نضمن خلوّها من الأخطاء ولا نتحمّل مسؤولية أي قرار اتُّخذ بناءً عليها.</p>
        </div>
    @else
        <h1 class="text-3xl font-extrabold text-gray-900">Terms of Use</h1>
        <div class="article-body mt-6">
            <p>By using this site you agree to the following terms.</p>
            <h2>Nature of content</h2>
            <p>Content is published for general educational purposes and is not personal financial, legal or tax advice. Consult a qualified professional before making financial decisions.</p>
            <h2>Intellectual property</h2>
            <p>All articles and designs are owned by us and may not be republished without permission.</p>
            <h2>Disclaimer</h2>
            <p>We strive to keep information accurate and up to date, but we do not guarantee it is error-free and accept no liability for decisions made based on it.</p>
        </div>
    @endif
</div>
@endsection
