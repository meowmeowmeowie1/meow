@php
    $loc = app()->getLocale();
    $disclosure = \App\Models\Setting::get('ai_disclosure_'.$loc);
@endphp
<footer class="mt-16 border-t border-gray-200 bg-white">
    <div class="mx-auto max-w-6xl px-4 py-10">
        <div class="flex flex-col gap-6 md:flex-row md:items-start md:justify-between">
            <div class="max-w-sm">
                <div class="text-lg font-bold text-brand-700">{{ $siteName }}</div>
                <p class="mt-2 text-sm text-gray-600">{{ __('messages.tagline') }}</p>
                @if ($disclosure)
                    <p class="mt-4 text-xs leading-relaxed text-gray-500">{{ $disclosure }}</p>
                @endif
            </div>

            <nav class="grid grid-cols-2 gap-x-10 gap-y-2 text-sm text-gray-600">
                <a href="{{ route('pages.about', ['locale' => $loc]) }}" class="hover:text-brand-700">{{ __('messages.about') }}</a>
                <a href="{{ route('pages.contact', ['locale' => $loc]) }}" class="hover:text-brand-700">{{ __('messages.contact') }}</a>
                <a href="{{ route('pages.privacy', ['locale' => $loc]) }}" class="hover:text-brand-700">{{ __('messages.privacy') }}</a>
                <a href="{{ route('pages.terms', ['locale' => $loc]) }}" class="hover:text-brand-700">{{ __('messages.terms') }}</a>
            </nav>
        </div>

        <div class="mt-8 border-t border-gray-100 pt-6 text-xs text-gray-500">
            © {{ date('Y') }} {{ $siteName }}. {{ __('messages.all_rights') }}
        </div>
    </div>
</footer>
