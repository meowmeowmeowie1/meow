@extends('layouts.app')

@section('content')
<div class="mx-auto max-w-xl">
    <h1 class="text-3xl font-extrabold text-gray-900">{{ __('messages.contact') }}</h1>
    <p class="mt-3 text-gray-600">
        {{ $locale === 'ar' ? 'نسعد بأسئلتك وملاحظاتك. اكتب لنا وسنرد في أقرب وقت.' : 'We’d love to hear your questions and feedback. Send us a note and we’ll reply soon.' }}
    </p>

    @if ($email = \App\Models\Setting::get('contact_email'))
        <p class="mt-2 text-sm text-gray-500">{{ __('messages.email') }}: <a class="text-brand-700 underline" href="mailto:{{ $email }}">{{ $email }}</a></p>
    @endif

    <form action="{{ route('pages.contact.submit', ['locale' => $locale]) }}" method="post" class="mt-8 space-y-4">
        @csrf
        <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">{{ __('messages.name') }}</label>
            <input name="name" value="{{ old('name') }}" required
                class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
            @error('name')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">{{ __('messages.email') }}</label>
            <input type="email" name="email" value="{{ old('email') }}" required
                class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
            @error('email')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">{{ __('messages.message') }}</label>
            <textarea name="message" rows="5" required
                class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">{{ old('message') }}</textarea>
            @error('message')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
        </div>
        <button class="rounded-full bg-brand-600 px-6 py-2.5 font-medium text-white hover:bg-brand-700">
            {{ __('messages.send') }}
        </button>
    </form>
</div>
@endsection
