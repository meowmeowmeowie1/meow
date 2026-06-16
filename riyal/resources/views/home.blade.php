@extends('layouts.app')

@section('content')
    <section class="mb-10">
        <h1 class="text-3xl font-extrabold tracking-tight text-gray-900 md:text-4xl">{{ $siteName ?? config('app.name') }}</h1>
        <p class="mt-3 max-w-2xl text-lg text-gray-600">{{ __('messages.tagline') }}</p>
    </section>

    @if ($featured->isNotEmpty())
        <section class="mb-12">
            <h2 class="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">{{ __('messages.featured') }}</h2>
            <div class="grid gap-6 md:grid-cols-3">
                @foreach ($featured as $post)
                    <x-post-card :post="$post" />
                @endforeach
            </div>
        </section>
    @endif

    @if ($categories->isNotEmpty())
        <section class="mb-10 flex flex-wrap gap-2">
            @foreach ($categories as $cat)
                <a href="{{ $cat->url() }}"
                    class="rounded-full border border-gray-300 px-3 py-1 text-sm text-gray-700 hover:border-brand-600 hover:text-brand-700">
                    {{ $cat->name }} <span class="text-gray-400">({{ $cat->posts_count }})</span>
                </a>
            @endforeach
        </section>
    @endif

    <section>
        <h2 class="mb-4 text-xl font-bold text-gray-900">{{ __('messages.latest_articles') }}</h2>
        @if ($latest->isEmpty() && $featured->isEmpty())
            <p class="rounded-xl border border-dashed border-gray-300 bg-white p-8 text-center text-gray-500">
                {{ __('messages.nothing_here') }}
            </p>
        @else
            <div class="grid gap-6 md:grid-cols-3">
                @foreach ($latest as $post)
                    <x-post-card :post="$post" />
                @endforeach
            </div>
        @endif
    </section>
@endsection
