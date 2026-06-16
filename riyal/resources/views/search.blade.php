@extends('layouts.app')

@section('content')
    <header class="mb-8">
        <h1 class="text-3xl font-extrabold text-gray-900">{{ __('messages.search') }}</h1>
        <form action="{{ route('search', ['locale' => $locale]) }}" method="get" class="mt-4 flex gap-2">
            <input type="search" name="q" value="{{ $q }}" autofocus
                placeholder="{{ __('messages.search_placeholder') }}"
                class="w-full max-w-md rounded-full border border-gray-300 px-5 py-2.5 focus:border-brand-600 focus:outline-none">
            <button class="rounded-full bg-brand-600 px-6 py-2.5 font-medium text-white hover:bg-brand-700">
                {{ __('messages.search') }}
            </button>
        </form>
    </header>

    @if ($q !== '')
        <p class="mb-6 text-gray-600">{{ __('messages.search_results_for') }}: <strong>{{ $q }}</strong></p>

        @if ($posts->isEmpty())
            <p class="rounded-xl border border-dashed border-gray-300 bg-white p-8 text-center text-gray-500">
                {{ __('messages.no_results') }}
            </p>
        @else
            <div class="grid gap-6 md:grid-cols-3">
                @foreach ($posts as $post)
                    <x-post-card :post="$post" />
                @endforeach
            </div>
            <div class="mt-8">{{ $posts->links() }}</div>
        @endif
    @endif
@endsection
