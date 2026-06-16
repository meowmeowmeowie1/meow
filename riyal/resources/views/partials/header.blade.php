@php
    $loc = app()->getLocale();
    $other = $loc === 'ar' ? 'en' : 'ar';
    // Switch to the counterpart page if one exists, else the other-locale home.
    $switchUrl = $alternates[$other] ?? route('home', ['locale' => $other]);

    $navCategories = \App\Models\Category::where('locale', $loc)
        ->withCount(['posts' => fn ($q) => $q->published()])
        ->orderByDesc('posts_count')
        ->take(6)
        ->get();
@endphp
<header class="border-b border-gray-200 bg-white">
    <div class="mx-auto flex max-w-6xl items-center gap-4 px-4 py-4">
        <a href="{{ route('home', ['locale' => $loc]) }}" class="flex items-center gap-2 text-xl font-bold text-brand-700">
            <span class="inline-grid h-9 w-9 place-items-center rounded-lg bg-brand-600 text-white">ر</span>
            <span>{{ $siteName }}</span>
        </a>

        <nav class="hidden items-center gap-5 text-sm font-medium text-gray-600 md:flex">
            <a href="{{ route('home', ['locale' => $loc]) }}" class="hover:text-brand-700">{{ __('messages.home') }}</a>
            @foreach ($navCategories as $cat)
                <a href="{{ $cat->url() }}" class="hover:text-brand-700">{{ $cat->name }}</a>
            @endforeach
        </nav>

        <div class="flex items-center gap-3 ms-auto">
            <form action="{{ route('search', ['locale' => $loc]) }}" method="get" class="hidden sm:block">
                <input type="search" name="q" value="{{ request('q') }}"
                    placeholder="{{ __('messages.search_placeholder') }}"
                    class="w-44 rounded-full border border-gray-300 px-4 py-1.5 text-sm focus:border-brand-600 focus:outline-none">
            </form>

            <a href="{{ $switchUrl }}"
                class="rounded-full border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:border-brand-600 hover:text-brand-700">
                {{ __('messages.switch_lang') }}
            </a>
        </div>
    </div>
</header>
